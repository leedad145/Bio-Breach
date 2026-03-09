// =============================================================================
// EnemyController.cs - 면역 세포 적 유닛
//
// 변경 사항:
//  - CharacterController로 물리적 이동 (중력 적용, 지형 충돌)
//  - WorldManager에 Viewer로 등록 → 주변 1청크 항상 로드
//  - 이동 경로 앞에 Voxel(MeshCollider) 감지 → 공격으로 제거 후 이동
//  - NetworkVariable<Vector3/float>로 위치·회전을 서버→클라이언트 동기화
// =============================================================================

using System;
using UnityEngine;
using VContainer;
using BioBreach.Controller.Enemy.Base;
using BioBreach.Engine.Entity;
using BioBreach.Engine.Data;
using BioBreach.Systems;
using BioBreach.Core.Voxel;
using BioBreach.Controller.Shared;
using Unity.Netcode;

namespace BioBreach.Controller.Enemy
{

    [RequireComponent(typeof(CharacterController))]
    public class EnemyController : EntityMonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("JSON 데이터")]
        [Tooltip("enemies.json 의 id. 반드시 입력해야 스탯이 적용된다.")]
        public string dataId;

        [Header("참조")]
        public LayerMask  defenseLayer;
        public Transform  matriarchTarget;
        public WorldManager worldManager;
        ////////////////////////////
        EnemyBrain brain;
        EnemyNavigation navigation;
        EnemyAction action;
        public EntityMonoBehaviour CurrentTarget => _currentTarget;

        // ── 스탯 (JSON에서 로드, Inspector 비공개) ──────────────────────────────
        [HideInInspector] public float           moveSpeed      = 5f;
        [HideInInspector] public float           gravityMultiplier = 2f;
        [HideInInspector] public float           detectionRange = 15f;
        [HideInInspector] public float           attackRange    = 2f;
        [HideInInspector] public float           attackDamage   = 10f;
        [HideInInspector] public float           attackCooldown = 1f;
        [HideInInspector] public TargetPriority  targetPriority = TargetPriority.Nearest;
        [HideInInspector] public float           digRadius      = 3f;
        [HideInInspector] public float           digStrength    = 2f;
        [HideInInspector] public float           jumpSpeed      = 7f;
        [HideInInspector] public float[]         jumpAngles     = { 20f, 35f, 50f };

        // =====================================================================
        // 스포너 스케일링 (EnemySpawner가 Start 이전에 주입)
        // =====================================================================

        [HideInInspector] public float hpMultiplier     = 1f;
        [HideInInspector] public float damageMultiplier = 1f;
        [HideInInspector] public float speedMultiplier  = 1f;
        [HideInInspector] public EnemySpawner homeSpawner;

        // =====================================================================
        // 적 타입 특수 능력 (JSON에서 로드)
        // =====================================================================

        enum EnemyType { Normal, Tanker, Exploder, Acid, Healer }
        EnemyType _enemyType = EnemyType.Normal;

        // Tanker & Exploder
        float _explosionRadius = 5f;
        float _explosionDamage = 35f;
        // Acid
        float _acidInterval    = 2f;
        float _acidRadius      = 3f;
        float _acidDigStrength = 2f;
        float _acidTimer;
        // Healer
        float _healRadius      = 8f;
        float _healPerSecond   = 8f;
        float _healCooldown    = 2f;
        float _healTimer;

        // =====================================================================
        // 네트워크 위치 동기화 (Server → 모든 클라이언트)
        // =====================================================================

        private readonly NetworkVariable<Vector3> _netPos = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _netYaw = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // =====================================================================
        // 내부 변수
        // =====================================================================

        CharacterController _cc;
        float               _velocityY;
        float               _lastActionTime;   // 공격 + 파기 공유 쿨타임
        EntityMonoBehaviour _currentTarget;
        EnemyRepository     _enemyRepo;
        float _lastJumpTime;

        const float JUMP_COOLDOWN = 0.8f;

        const float RAY_LOW = 0.3f;
        const float RAY_MID = 1.0f;
        const float RAY_HIGH = 1.8f;

        // ── 최적화용 캐시 ──────────────────────────────────────────────────────
        float   _lastTargetTime  = -999f;
        const float TARGET_INTERVAL = 0.25f;   // 타겟 선택 간격 (초)

        float   _blockCacheTime  = -999f;
        const float BLOCK_INTERVAL  = 0.1f;    // SphereCast 캐시 유효 시간 (초)
        bool    _cachedBlocking;
        Vector3 _cachedBlockPoint;

        [Inject]
        public void Construct(EnemyRepository enemyRepo) => _enemyRepo = enemyRepo;

        // =====================================================================
        // 초기화
        // =====================================================================

        protected override void Start()
        {
            // JSON 데이터 적용 (Inspector 기본값을 덮어씀)
            // OnNetworkSpawn 이전에 maxHp 스케일링을 반영하기 위해 Start에서 처리
            if (!string.IsNullOrEmpty(dataId) && _enemyRepo != null)
            {
                if (_enemyRepo.TryGet(dataId, out var d))
                {
                    maxHp             = d.maxHp;
                    moveSpeed         = d.moveSpeed;
                    gravityMultiplier = d.gravityMultiplier;
                    detectionRange    = d.detectionRange;
                    attackRange       = d.attackRange;
                    attackDamage      = d.attackDamage;
                    attackCooldown    = d.attackCooldown;
                    targetPriority    = Enum.Parse<TargetPriority>(d.targetPriority, ignoreCase: true);
                    digRadius         = d.digRadius;
                    digStrength       = d.digStrength;

                    if (Enum.TryParse(d.enemyType, ignoreCase: true, out EnemyType et))
                        _enemyType = et;
                    _explosionRadius  = d.explosionRadius;
                    _explosionDamage  = d.explosionDamage;
                    _acidInterval     = d.acidInterval;
                    _acidRadius       = d.acidRadius;
                    _acidDigStrength  = d.acidDigStrength;
                    _healRadius       = d.healRadius;
                    _healPerSecond    = d.healPerSecond;
                    _healCooldown     = d.healCooldown;
                }
            }

            maxHp        *= hpMultiplier;
            attackDamage *= damageMultiplier;
            moveSpeed    *= speedMultiplier;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn(); // EntityMonoBehaviour HP 초기화

            _cc = GetComponent<CharacterController>();
            brain = new EnemyBrain(this);
            navigation = new EnemyNavigation();
            action = new EnemyAction(this);

            if (!IsServer)
            {
                // 클라이언트는 CharacterController를 끄고 NetworkVariable 위치로만 이동
                _cc.enabled = false;
                return;
            }

            if (worldManager == null)
                worldManager = FindAnyObjectByType<WorldManager>();

            // 뷰어 등록은 스포너가 담당 — 적 개체는 개별 등록하지 않음

            // 스폰 직후 현재 위치를 기록 (클라이언트가 (0,0,0)에서 튀는 것 방지)
            _netPos.Value = transform.position;
            _netYaw.Value = transform.eulerAngles.y;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer)
            {
                homeSpawner?.ReportEnemyDied(this);
                // Exploder: 사망 시 범위 폭발
                if (!IsAlive && _enemyType == EnemyType.Exploder)
                    ExplodeOnDeath();
            }
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            if (!IsSpawned) return;

            if (!IsServer)
            {
                if (_netPos.Value.sqrMagnitude < 0.001f) return;

                transform.SetPositionAndRotation(
                    Vector3.Lerp(transform.position, _netPos.Value, Time.deltaTime * 15f),
                    Quaternion.Slerp(transform.rotation,
                        Quaternion.Euler(0f, _netYaw.Value, 0f),
                        Time.deltaTime * 15f));

                return;
            }

            if (!IsAlive) return;

            if (Time.time - _lastTargetTime >= TARGET_INTERVAL)
            {
                _currentTarget = SelectTarget();
                _lastTargetTime = Time.time;
            }

            brain.UpdateBrain();

            // AI 행동 전략은 EnemyAction.Tick()이 상태별로 결정
            action.Tick(brain.CurrentState);

            ApplyGravity();
            UpdateTypeAbility();

            Vector3 pos = transform.position;
            if ((pos - _netPos.Value).sqrMagnitude > 0.01f)
                _netPos.Value = pos;

            float yaw = transform.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, _netYaw.Value)) > 0.5f)
                _netYaw.Value = yaw;
        }

        // =====================================================================
        // 중력
        // =====================================================================

        void ApplyGravity()
        {
            if (_cc.isGrounded && _velocityY < 0f)
                _velocityY = -2f;

            _velocityY += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
            _cc.Move(Vector3.up * _velocityY * Time.deltaTime);
        }

        // =====================================================================
        // 타겟 선택 — TargetSelector 공유 유틸리티 위임
        // =====================================================================

        EntityMonoBehaviour SelectTarget()
        {
            var target = TargetSelector.FindTarget(
                transform.position,
                detectionRange,
                defenseLayer,
                targetPriority
            );

            if (target != null)
                return target;

            if (matriarchTarget != null)
                return matriarchTarget.GetComponent<EntityMonoBehaviour>();

            return null;
        }
        // =====================================================================
        // 이동 & Voxel 장애물 처리
        // =====================================================================
        public bool IsTargetInAttackRange(EntityMonoBehaviour target)
        {
            return (transform.position - target.transform.position).sqrMagnitude
                <= attackRange * attackRange;
        }
        public bool IsPathBlocked()
        {
            Vector3 dir = transform.forward;
            return IsVoxelBlocking(dir, out _);
        }
        
        public void MoveToward(Vector3 targetPos)
        {
            Vector3 diff = targetPos - transform.position;

            if (diff.sqrMagnitude <= attackRange * attackRange)
                return;

            Vector3 dir = new Vector3(diff.x, 0, diff.z).normalized;

            if (dir.sqrMagnitude < 0.001f)
                return;

            if (CheckObstacle(dir, out ObstacleType type))
            {
                if (type == ObstacleType.Jump)
                {
                    TryJump();
                }
                else if (type == ObstacleType.Wall)
                {
                    TryDigToward(diff);
                }
            }
            else
            {
                _cc.Move(dir * moveSpeed * Time.deltaTime);
            }

            transform.rotation = Quaternion.LookRotation(dir);
        }
        enum ObstacleType
        {
            None,
            Jump,
            Wall
        }
        bool CheckObstacle(Vector3 dir, out ObstacleType type)
        {
            type = ObstacleType.None;

            float dist = _cc.radius + 0.5f;

            Vector3 basePos = transform.position;

            bool low =
                Physics.Raycast(basePos + Vector3.up * RAY_LOW, dir, dist);

            bool mid =
                Physics.Raycast(basePos + Vector3.up * RAY_MID, dir, dist);

            bool high =
                Physics.Raycast(basePos + Vector3.up * RAY_HIGH, dir, dist);

            if (!low)
                return false;

            if (low && !mid)
                return false;

            if (low && mid && !high)
            {
                type = ObstacleType.Jump;
                return true;
            }

            if (low && mid && high)
            {
                type = ObstacleType.Wall;
                return true;
            }

            return false;
        }
        

        void TryJump()
        {
            if (!_cc.isGrounded)
                return;

            if (Time.time - _lastJumpTime < JUMP_COOLDOWN)
                return;

            _velocityY = jumpSpeed;
            _lastJumpTime = Time.time;
        }

        /// <summary>
        /// diff 방향으로 Voxel이 막혀 있으면 파낸다. MoveToward에서만 호출.
        /// 수평 + 수직 두 SphereCast로 위아래 장애물도 처리한다.
        /// </summary>
        public void TryDigToward(Vector3 diff)
        {
            if (worldManager == null || digStrength <= 0f) return;
            if (Time.time - _lastActionTime < attackCooldown) return;

            Vector3 hDir = new Vector3(diff.x, 0f, diff.z).normalized;
            Vector3 vDir = new Vector3(0f, diff.y, 0f).normalized;

            bool dug = false;
            if (hDir.sqrMagnitude > 0.001f && IsVoxelBlocking(hDir, out Vector3 hp))
            {
                worldManager.ModifyTerrain(hp, digRadius, digStrength, VoxelType.Air);
                dug = true;
            }
            if (vDir.sqrMagnitude > 0.001f && IsVoxelBlocking(vDir, out Vector3 vp))
            {
                worldManager.ModifyTerrain(vp, digRadius, digStrength, VoxelType.Air);
                dug = true;
            }

            if (dug) _lastActionTime = Time.time;
        }

        /// <summary>
        /// 이동 방향으로 SphereCast(반경 = digRadius) 후 Voxel(MeshCollider)이 막고 있으면 true.
        /// <paramref name="blockPoint"/>: 파기 목표점 (Voxel 내부 약간).
        /// </summary>
        bool IsVoxelBlocking(Vector3 dir, out Vector3 blockPoint)
        {
            blockPoint = Vector3.zero;

            float checkDist = _cc.radius + 1.5f;
            Vector3 origin    = transform.position + _cc.center;

            if (!Physics.SphereCast(origin, digRadius, dir, out RaycastHit hit, checkDist,
                    ~0, QueryTriggerInteraction.Ignore))
                return false;

            // MeshCollider = 청크 지형, 그 외는 엔티티 또는 기타 콜라이더
            if (hit.collider is not MeshCollider) return false;

            blockPoint = hit.point - hit.normal * 0.1f; // Voxel 내부 진입점
            return true;
        }

        // =====================================================================
        // 타입별 특수 능력
        // =====================================================================

        void UpdateTypeAbility()
        {
            switch (_enemyType)
            {
                case EnemyType.Acid:   UpdateAcid();   break;
                case EnemyType.Healer: UpdateHealer(); break;
            }
        }

        /// <summary>Acid: 주기적으로 주변 Voxel 지형을 부식</summary>
        void UpdateAcid()
        {
            _acidTimer += Time.deltaTime;
            if (_acidTimer < _acidInterval) return;
            _acidTimer = 0f;
            worldManager?.ModifyTerrain(transform.position, _acidRadius, _acidDigStrength, VoxelType.Air);
        }

        /// <summary>Healer: 주기적으로 주변 적군 HP 회복</summary>
        void UpdateHealer()
        {
            _healTimer += Time.deltaTime;
            if (_healTimer < _healCooldown) return;
            _healTimer = 0f;

            float healAmount = _healPerSecond * _healCooldown;
            float radiusSq   = _healRadius * _healRadius;

            // 스포너의 리스트를 우선 사용 — FindObjectsByType 씬 전체 스캔 방지
            var allies = homeSpawner != null
                ? homeSpawner.ActiveEnemyList
                : null;

            if (allies == null) return;

            for (int i = allies.Count - 1; i >= 0; i--)
            {
                var ally = allies[i];
                if (ally == null) { allies.RemoveAt(i); continue; }
                if (ally == this || !ally.IsAlive) continue;
                if ((ally.transform.position - transform.position).sqrMagnitude > radiusSq) continue;
                ally.Heal(healAmount);
            }
        }

        /// <summary>Tanker: 공격 시 주변 범위 피해 (AOE 슬램)</summary>
        void TryAoeStomp()
        {
            var hits = Physics.OverlapSphere(transform.position, _explosionRadius, defenseLayer);
            foreach (var hit in hits)
            {
                var entity = hit.GetComponent<EntityMonoBehaviour>();
                if (entity != null && entity.IsAlive)
                    entity.TakeDamage(_explosionDamage);
            }
        }

        /// <summary>Exploder: 사망 시 범위 폭발</summary>
        void ExplodeOnDeath()
        {
            var hits = Physics.OverlapSphere(transform.position, _explosionRadius, defenseLayer);
            foreach (var hit in hits)
            {
                var entity = hit.GetComponent<EntityMonoBehaviour>();
                if (entity != null && entity.IsAlive)
                    entity.TakeDamage(_explosionDamage);
            }
        }

        // =====================================================================
        // 공격
        // =====================================================================

        public void TryAttack(EntityMonoBehaviour target)
        {
            if (target == null || !target.IsAlive) return;
            if ((transform.position - target.transform.position).sqrMagnitude > attackRange * attackRange) return;
            if (Time.time - _lastActionTime < attackCooldown) return;

            target.TakeDamage(attackDamage);

            // Tanker: 공격마다 AOE 슬램 추가
            if (_enemyType == EnemyType.Tanker)
                TryAoeStomp();

            _lastActionTime = Time.time;
        }

        // =====================================================================
        // 기즈모 (에디터 전용)
        // =====================================================================

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            if (_cc == null)
                _cc = GetComponent<CharacterController>();

            Vector3 dir = transform.forward;
            float dist = _cc != null ? _cc.radius + 0.8f : 1f;

            Vector3 basePos = transform.position;

            // LOW RAY (발)
            Gizmos.color = Color.red;
            Gizmos.DrawRay(basePos + Vector3.up * 0.3f, dir * dist);

            // MID RAY (가슴)
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(basePos + Vector3.up * 1.0f, dir * dist);

            // HIGH RAY (머리)
            Gizmos.color = Color.green;
            Gizmos.DrawRay(basePos + Vector3.up * 1.8f, dir * dist);
        }
    }
}
