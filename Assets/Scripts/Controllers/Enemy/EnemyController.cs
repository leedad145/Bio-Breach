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

        // ── Stuck 감지 ──────────────────────────────────────────────────────────
        Vector3 _stuckCheckPos;
        float   _stuckCheckTimer;
        bool    _isStuck;
        const float STUCK_CHECK_INTERVAL = 0.3f;
        const float STUCK_MIN_DIST_SQ    = 0.09f;

        // ── 파기 전용 쿨타임 (공격 쿨타임과 분리) ──────────────────────────────
        float       _lastDigTime;
        const float DIG_COOLDOWN = 0.4f;

        const float RAY_LOW  = -0.5f; // 지면 바로 위 — 낮은 단차 확실히 감지
        const float RAY_MID  = 0f;  // 1 voxel 내부 중심 — Jump/Wall 분기점
        const float RAY_HIGH = 0.5f;  // 1 voxel 위(>1.0), 2 voxel 내부(<2.0) — Wall 판정

        // ── 최적화용 캐시 ──────────────────────────────────────────────────────
        float   _lastTargetTime  = -999f;
        const float TARGET_INTERVAL = 0.25f;   // 타겟 선택 간격 (초)

        [Inject]
        public void Construct(EnemyRepository enemyRepo) => _enemyRepo = enemyRepo;

        // =====================================================================
        // 초기화
        // =====================================================================

        protected override void Start()
        {
            base.Start();
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
        public void MoveToward(Vector3 targetPos)
        {
            Vector3 diff = targetPos - transform.position;

            if (diff.sqrMagnitude <= attackRange * attackRange)
                return;

            Vector3 dir = new Vector3(diff.x, 0, diff.z).normalized;

            if (dir.sqrMagnitude < 0.001f)
                return;

            // ── Stuck 감지: 0.3초마다 위치 변화 측정 ─────────────────────────
            _stuckCheckTimer += Time.deltaTime;
            if (_stuckCheckTimer >= STUCK_CHECK_INTERVAL)
            {
                _isStuck = (transform.position - _stuckCheckPos).sqrMagnitude < STUCK_MIN_DIST_SQ;
                _stuckCheckPos  = transform.position;
                _stuckCheckTimer = 0f;
            }

            bool obstacleDetected = CheckObstacle(dir, out ObstacleType type);

            // Stuck 상태: 무조건 파기
            if (_isStuck)
            {
                type             = ObstacleType.Wall;
                obstacleDetected = true;
                _isStuck         = false;
            }

            if (obstacleDetected)
            {
                if (type == ObstacleType.Jump)
                {
                    TryJump();
                    _cc.Move(dir * moveSpeed * Time.deltaTime);
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

            // defenseLayer 제외: 방어 유닛을 지형 장애물로 오인하지 않음
            int terrainMask = ~defenseLayer;

            bool low  = Physics.Raycast(basePos + Vector3.up * RAY_LOW,  dir, dist, terrainMask);
            bool mid  = Physics.Raycast(basePos + Vector3.up * RAY_MID,  dir, dist, terrainMask);
            bool high = Physics.Raycast(basePos + Vector3.up * RAY_HIGH, dir, dist, terrainMask);

            if (low && mid && !high) { type = ObstacleType.Jump; return true; }
            if (low && mid && high)  { type = ObstacleType.Wall; return true; }

            // low && !mid: stepOffset 범위 내 낮은 단차 → CC가 자동 처리
            // !low: 장애물 없음
            // 나머지 엣지케이스는 Stuck 감지(CapsuleCast)가 처리
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
            if (Time.time - _lastDigTime < DIG_COOLDOWN) return; // 공격 쿨타임과 분리

            Vector3 hDir = new Vector3(diff.x, 0f, diff.z).normalized;
            if (hDir.sqrMagnitude < 0.001f) return;

            // 레이캐스트 없이 몸 앞 _cc.radius 거리에서 3개 높이를 무조건 파냄.
            // 공기든 지형이든 관계없이 파기 → 작은 구멍도 점점 커짐.
            // center.y=0 기준: 발(0) ~ 머리(RAY_HIGH)를 세 높이로 커버
            float digForward = _cc.radius + 0.3f;
            float[] heights  = { RAY_LOW, RAY_MID, RAY_HIGH };

            foreach (float h in heights)
            {
                Vector3 point = transform.position + Vector3.up * h + hDir * digForward;
                worldManager.ModifyTerrain(point, digRadius, digStrength, VoxelType.Air);
            }

            _lastDigTime = Time.time;
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
        void ExplodeOnDeath() => TryAoeStomp();

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
            Gizmos.DrawRay(basePos + Vector3.up * RAY_LOW, dir * dist);

            // MID RAY (가슴)
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(basePos + Vector3.up * RAY_MID, dir * dist);

            // HIGH RAY (머리)
            Gizmos.color = Color.green;
            Gizmos.DrawRay(basePos + Vector3.up * RAY_HIGH, dir * dist);
        }
    }
}
