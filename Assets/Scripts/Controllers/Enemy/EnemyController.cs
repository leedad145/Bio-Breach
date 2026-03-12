// =============================================================================
// EnemyController.cs - 면역 세포 적 유닛
//
// 이동: CharacterController(중력·충돌) + NavMeshAgent(경로탐색, position 제어 OFF)
//       NavMeshAgent.desiredVelocity → CharacterController.Move
//       NavMesh가 없는 구간(낙하·지형 파괴 직후)은 수동 중력으로 처리
//
// 최적화:
//   - AI 스태거링 : AI_STRIDE(4) 프레임마다 순번(slot)으로 분산
//   - 타겟 선택   : 0.5 초 간격 + 랜덤 지터(동시 쿼리 방지)
//   - NetworkSync : sqrMagnitude 임계값 초과 시만 기록
// =============================================================================

using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;
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
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : EntityMonoBehaviour
    {
        // =====================================================================
        // Inspector
        // =====================================================================

        [Header("JSON 데이터")]
        [Tooltip("enemies.json 의 id")]
        public string dataId;

        [Header("참조")]
        public LayerMask  defenseLayer;
        public Transform  matriarchTarget;
        public WorldManager worldManager;

        // ── 스탯 (JSON 로드) ────────────────────────────────────────────────
        [HideInInspector] public float          moveSpeed         = 5f;
        [HideInInspector] public float          gravityMultiplier = 2f;
        [HideInInspector] public float          detectionRange    = 15f;
        [HideInInspector] public float          attackRange       = 2f;
        [HideInInspector] public float          attackDamage      = 10f;
        [HideInInspector] public float          attackCooldown    = 1f;
        [HideInInspector] public TargetPriority targetPriority    = TargetPriority.Nearest;
        [HideInInspector] public float          digRadius         = 3f;
        [HideInInspector] public float          digStrength       = 2f;

        // ── 스포너 스케일링 ─────────────────────────────────────────────────
        [HideInInspector] public float hpMultiplier     = 1f;
        [HideInInspector] public float damageMultiplier = 1f;
        [HideInInspector] public float speedMultiplier  = 1f;
        [HideInInspector] public EnemySpawner homeSpawner;

        // ── 적 타입 능력 ────────────────────────────────────────────────────
        enum EnemyType { Normal, Tanker, Exploder, Acid, Healer }
        EnemyType _enemyType = EnemyType.Normal;

        float _explosionRadius = 5f;
        float _explosionDamage = 35f;
        float _acidInterval    = 2f;
        float _acidRadius      = 3f;
        float _acidDigStrength = 2f;
        float _acidTimer;
        float _healRadius      = 8f;
        float _healPerSecond   = 8f;
        float _healCooldown    = 2f;
        float _healTimer;

        // ── 네트워크 위치 동기화 ────────────────────────────────────────────
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
        NavMeshAgent        _agent;
        EnemyBrain          _brain;
        EnemyAction         _action;

        float               _velocityY;
        float               _lastAttackTime;
        float               _lastDigTime;
        EntityMonoBehaviour _currentTarget;
        EnemyRepository     _enemyRepo;

        const float DIG_COOLDOWN = 0.4f;

        // ── 타겟 선택 ───────────────────────────────────────────────────────
        float _lastTargetTime = -999f;
        const float TARGET_INTERVAL = 0.5f;

        // ── AI 스태거링 ─────────────────────────────────────────────────────
        // 전체 적을 AI_STRIDE 그룹으로 분산 → 매 프레임 1/AI_STRIDE만 풀 AI 실행
        const int AI_STRIDE = 4;
        int       _aiSlot;
        static int _slotCounter;

        // 공개 프로퍼티 (EnemyAction/EnemyBrain 접근용)
        public EntityMonoBehaviour CurrentTarget => _currentTarget;

        [Inject]
        public void Construct(EnemyRepository enemyRepo) => _enemyRepo = enemyRepo;

        // =====================================================================
        // 초기화
        // =====================================================================

        protected override void Start()
        {
            base.Start();

            if (!string.IsNullOrEmpty(dataId) && _enemyRepo != null &&
                _enemyRepo.TryGet(dataId, out var d))
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

                if (Enum.TryParse(d.enemyType, ignoreCase: true, out EnemyType et)) _enemyType = et;
                _explosionRadius  = d.explosionRadius;
                _explosionDamage  = d.explosionDamage;
                _acidInterval     = d.acidInterval;
                _acidRadius       = d.acidRadius;
                _acidDigStrength  = d.acidDigStrength;
                _healRadius       = d.healRadius;
                _healPerSecond    = d.healPerSecond;
                _healCooldown     = d.healCooldown;
            }

            maxHp        *= hpMultiplier;
            attackDamage *= damageMultiplier;
            moveSpeed    *= speedMultiplier;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _cc    = GetComponent<CharacterController>();
            _agent = GetComponent<NavMeshAgent>();
            _brain = new EnemyBrain(this);
            _action = new EnemyAction(this);

            if (!IsServer)
            {
                // 클라이언트: NavMeshAgent·CharacterController 모두 꺼고
                //             NetworkVariable 위치로만 보간
                _cc.enabled    = false;
                _agent.enabled = false;
                return;
            }

            if (worldManager == null)
                worldManager = FindAnyObjectByType<WorldManager>();

            // NavMeshAgent 하이브리드 설정
            // updatePosition/Rotation = false → 우리가 직접 CC로 이동시킴
            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.stoppingDistance = attackRange * 0.9f;
            _agent.speed = moveSpeed;    // 참조용. 실제 속도는 CC.Move로 제어
            _agent.avoidancePriority = UnityEngine.Random.Range(30, 70); // 에이전트별 회피 우선순위 랜덤화

            // AI 업데이트 슬롯 배정
            _aiSlot = Interlocked.Increment(ref _slotCounter) % AI_STRIDE;

            // 초기 위치 동기화
            _netPos.Value = transform.position;
            _netYaw.Value = transform.eulerAngles.y;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (!IsServer) return;

            if (homeSpawner != null) homeSpawner.ReportEnemyDied(this);
            if (!IsAlive && _enemyType == EnemyType.Exploder)
                ExplodeOnDeath();
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            if (!IsSpawned) return;

            // ── 클라이언트: NetworkVariable 위치로 보간 ──────────────────
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

            // ── 스태거링된 AI 로직 (target 선택 + brain + action) ────────
            if (Time.frameCount % AI_STRIDE == _aiSlot)
            {
                if (Time.time - _lastTargetTime >= TARGET_INTERVAL)
                {
                    _currentTarget  = SelectTarget();
                    _lastTargetTime = Time.time;
                }
                _brain.UpdateBrain();
                _action.Tick(_brain.CurrentState);
            }

            // ── 이동 적용 (매 프레임) ────────────────────────────────────
            ApplyNavMovement();
            ApplyGravity();
            UpdateTypeAbility();

            // ── NavMesh 에이전트 위치 동기화 ─────────────────────────────
            if (_agent.enabled && _agent.isOnNavMesh)
                _agent.nextPosition = transform.position;

            // ── 네트워크 위치 기록 ────────────────────────────────────────
            Vector3 pos = transform.position;
            if ((pos - _netPos.Value).sqrMagnitude > 0.04f)  // 0.2m 이상 이동 시만 기록
                _netPos.Value = pos;

            float yaw = transform.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, _netYaw.Value)) > 1f)
                _netYaw.Value = yaw;
        }

        // =====================================================================
        // 이동
        // =====================================================================

        /// <summary>NavMeshAgent.desiredVelocity를 CharacterController에 적용.</summary>
        void ApplyNavMovement()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

            Vector3 vel = _agent.desiredVelocity;
            vel.y = 0f;
            if (vel.sqrMagnitude < 0.01f) return;

            _cc.Move(vel.normalized * (moveSpeed * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(vel);
        }

        void ApplyGravity()
        {
            if (_cc == null || !_cc.enabled) return;

            if (_cc.isGrounded && _velocityY < 0f) _velocityY = -2f;
            _velocityY += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
            _cc.Move(Vector3.up * _velocityY * Time.deltaTime);
        }

        // =====================================================================
        // 공개 NavMesh 헬퍼 (EnemyAction에서 호출)
        // =====================================================================

        /// <summary>NavMesh 목적지 설정. NavMesh가 없으면 무시.</summary>
        public void SetNavDestination(Vector3 dest)
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
            // 목적지가 크게 달라졌을 때만 SetDestination (내부 경로 재계산 비용 절감)
            if (!_agent.hasPath || (dest - _agent.destination).sqrMagnitude > 1f)
                _agent.SetDestination(dest);
        }

        /// <summary>NavMesh 경로가 막혔거나 유효하지 않으면 true.</summary>
        public bool IsNavPathBlocked()
        {
            return _agent != null && _agent.enabled && _agent.isOnNavMesh &&
                   (_agent.pathStatus == NavMeshPathStatus.PathPartial ||
                    _agent.pathStatus == NavMeshPathStatus.PathInvalid);
        }

        /// <summary>공격 범위 내에 타겟이 있는지 확인.</summary>
        public bool IsTargetInAttackRange(EntityMonoBehaviour target)
        {
            return (transform.position - target.transform.position).sqrMagnitude
                   <= attackRange * attackRange;
        }

        // =====================================================================
        // Voxel 파기 (NavMesh 경로 막혔을 때 fallback)
        // =====================================================================

        public void TryDigToward(Vector3 diff)
        {
            if (worldManager == null || digStrength <= 0f) return;
            if (Time.time - _lastDigTime < DIG_COOLDOWN) return;

            Vector3 hDir = new Vector3(diff.x, 0f, diff.z).normalized;
            if (hDir.sqrMagnitude < 0.001f) return;

            float forward = (_cc != null ? _cc.radius : 0.5f) + 0.3f;
            foreach (float h in new[] { -0.5f, 0f, 0.5f })
            {
                Vector3 point = transform.position + Vector3.up * h + hDir * forward;
                worldManager.ModifyTerrain(point, digRadius, digStrength, VoxelType.Air);
            }
            _lastDigTime = Time.time;
        }

        // =====================================================================
        // 타겟 선택
        // =====================================================================

        EntityMonoBehaviour SelectTarget()
        {
            var t = TargetSelector.FindTarget(
                transform.position, detectionRange, defenseLayer, targetPriority);

            if (t != null) return t;
            return matriarchTarget != null
                ? matriarchTarget.GetComponent<EntityMonoBehaviour>()
                : null;
        }

        // =====================================================================
        // 공격
        // =====================================================================

        public void TryAttack(EntityMonoBehaviour target)
        {
            if (target == null || !target.IsAlive) return;
            if ((transform.position - target.transform.position).sqrMagnitude > attackRange * attackRange) return;
            if (Time.time - _lastAttackTime < attackCooldown) return;

            target.TakeDamage(attackDamage);
            if (_enemyType == EnemyType.Tanker) TryAoeStomp();
            _lastAttackTime = Time.time;
        }

        // =====================================================================
        // 타입 능력
        // =====================================================================

        void UpdateTypeAbility()
        {
            switch (_enemyType)
            {
                case EnemyType.Acid:   UpdateAcid();   break;
                case EnemyType.Healer: UpdateHealer(); break;
            }
        }

        void UpdateAcid()
        {
            _acidTimer += Time.deltaTime;
            if (_acidTimer < _acidInterval) return;
            _acidTimer = 0f;
            worldManager?.ModifyTerrain(transform.position, _acidRadius, _acidDigStrength, VoxelType.Air);
        }

        void UpdateHealer()
        {
            _healTimer += Time.deltaTime;
            if (_healTimer < _healCooldown) return;
            _healTimer = 0f;

            float healAmount = _healPerSecond * _healCooldown;
            float radiusSq   = _healRadius * _healRadius;
            var   allies     = homeSpawner != null ? homeSpawner.ActiveEnemyList : null;
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

        void ExplodeOnDeath() => TryAoeStomp();

        // =====================================================================
        // 기즈모
        // =====================================================================

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
