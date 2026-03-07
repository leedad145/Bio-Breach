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

            if (!IsServer)
            {
                // 클라이언트는 CharacterController를 끄고 NetworkVariable 위치로만 이동
                _cc.enabled = false;
                return;
            }

            if (worldManager == null)
                worldManager = FindAnyObjectByType<WorldManager>();

            // Server만 뷰어 등록 — AI 이동은 Server에서만 실행
            worldManager?.RegisterViewer(transform, 1);

            // 스폰 직후 현재 위치를 기록 (클라이언트가 (0,0,0)에서 튀는 것 방지)
            _netPos.Value = transform.position;
            _netYaw.Value = transform.eulerAngles.y;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer)
                worldManager?.UnregisterViewer(transform);
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                if (!IsAlive) return;

                _currentTarget = SelectTarget();

                if (_currentTarget != null)
                {
                    MoveToward(_currentTarget.transform.position);
                    TryAttack(_currentTarget);
                }
                else if (matriarchTarget != null)
                {
                    MoveToward(matriarchTarget.position);
                    var matriarch = matriarchTarget.GetComponent<EntityMonoBehaviour>();
                    if (matriarch != null) TryAttack(matriarch);
                }

                ApplyGravity();

                // 클라이언트에 위치·회전 동기화
                _netPos.Value = transform.position;
                _netYaw.Value = transform.eulerAngles.y;
            }
            else
            {
                // 클라이언트: 서버 위치·회전으로 보간
                if (_netPos.Value.sqrMagnitude < 0.001f) return;

                transform.SetPositionAndRotation(
                    Vector3.Lerp(transform.position, _netPos.Value, Time.deltaTime * 15f),
                    Quaternion.Slerp(transform.rotation,
                        Quaternion.Euler(0f, _netYaw.Value, 0f), Time.deltaTime * 15f));
            }
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
            => TargetSelector.FindTarget(transform.position, detectionRange, defenseLayer, targetPriority);

        // =====================================================================
        // 이동 & Voxel 장애물 처리
        // =====================================================================

        void MoveToward(Vector3 targetPos)
        {
            Vector3 diff = targetPos - transform.position;
            if (diff.sqrMagnitude <= attackRange * attackRange) return;

            // 수평 방향만 사용 (수직은 중력으로 처리)
            Vector3 dir = new Vector3(diff.x, 0f, diff.z).normalized;
            if (dir.sqrMagnitude < 0.001f) return;

            if (IsVoxelBlocking(dir, out _))
            {
                // 위쪽 각도 레이가 뚫려 있으면 점프, 전부 막히면 파기
                if (!TryJumpOver(dir))
                    TryDigToward(diff);
            }
            else
            {
                _cc.Move(dir * moveSpeed * Time.deltaTime);
            }

            transform.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>
        /// dir 기준으로 위쪽 각도 레이(jumpAngles)를 순서대로 쏜다.
        /// 하나라도 뚫려 있으면 점프하고 true 반환, 전부 막히면 false.
        /// </summary>
        bool TryJumpOver(Vector3 dir)
        {
            if (!_cc.isGrounded) return false;

            // dir이 수평이므로 Cross(dir, up)은 항상 수평 수직벡터 → 위로 회전하는 축
            Vector3 rotAxis = Vector3.Cross(dir, Vector3.up);

            foreach (float angle in jumpAngles)
            {
                Vector3 upDir = Quaternion.AngleAxis(angle, rotAxis) * dir;
                if (!IsVoxelBlocking(upDir, out _))
                {
                    _velocityY = jumpSpeed;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// diff 방향으로 Voxel이 막혀 있으면 파낸다. MoveToward에서만 호출.
        /// 수평 + 수직 두 SphereCast로 위아래 장애물도 처리한다.
        /// </summary>
        void TryDigToward(Vector3 diff)
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

            float   checkDist = _cc.radius + detectionRange;
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
        // 공격
        // =====================================================================

        void TryAttack(EntityMonoBehaviour target)
        {
            if (target == null || !target.IsAlive) return;
            if ((transform.position - target.transform.position).sqrMagnitude > attackRange * attackRange) return;
            if (Time.time - _lastActionTime < attackCooldown) return;

            target.TakeDamage(attackDamage);
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

            // 전방 감지 레이
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, transform.forward * (_cc != null ? _cc.radius + detectionRange : detectionRange));
        }
    }
}
