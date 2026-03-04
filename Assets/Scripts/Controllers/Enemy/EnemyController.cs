// =============================================================================
// EnemyController.cs - 면역 세포 적 유닛
//
// 변경 사항:
//  - CharacterController로 물리적 이동 (중력 적용, 지형 충돌)
//  - WorldManager에 Viewer로 등록 → 주변 1청크 항상 로드
//  - 이동 경로 앞에 Voxel(MeshCollider) 감지 → 공격으로 제거 후 이동
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BioBreach.Engine.Entity;
using BioBreach.Systems;
using BioBreach.Core.Voxel;

namespace BioBreach.Controller.Enemy
{
    [RequireComponent(typeof(CharacterController))]
    public class EnemyController : EntityMonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("이동")]
        public float moveSpeed = 5f;

        [Header("중력")]
        public float gravityMultiplier = 2f;

        [Header("탐지 & 공격")]
        public float detectionRange = 15f;
        public float attackRange    = 2f;
        public LayerMask defenseLayer;

        [Header("공격")]
        public float attackDamage   = 10f;
        public float attackCooldown = 1f;

        [Header("타겟 우선순위")]
        public TargetPriority targetPriority = TargetPriority.Nearest;

        [Header("기본 이동 목표")]
        public Transform matriarchTarget;

        [Header("Voxel 파기 (길 막힌 경우)")]
        [Tooltip("전방 Voxel 감지 거리 (CharacterController 반경 + 여유)")]
        public float digDetectDist = 1.5f;
        [Tooltip("Voxel 파기 반경")]
        public float digRadius     = 1.5f;
        [Tooltip("Voxel 파기 강도")]
        public float digStrength   = 0.5f;
        [Tooltip("WorldManager 참조 (null이면 자동 탐색)")]
        public WorldManager worldManager;

        // =====================================================================
        // 스포너 스케일링 (EnemySpawner가 Start 이전에 주입)
        // =====================================================================

        [HideInInspector] public float hpMultiplier     = 1f;
        [HideInInspector] public float damageMultiplier = 1f;
        [HideInInspector] public float speedMultiplier  = 1f;

        // =====================================================================
        // 내부 변수
        // =====================================================================

        CharacterController _cc;
        float               _velocityY;
        float               _lastAttackTime;
        EntityMonoBehaviour _currentTarget;

        // =====================================================================
        // 초기화
        // =====================================================================

        protected override void Start()
        {
            maxHp        *= hpMultiplier;
            attackDamage *= damageMultiplier;
            moveSpeed    *= speedMultiplier;

            base.Start();

            _cc = GetComponent<CharacterController>();

            if (worldManager == null)
                worldManager = FindAnyObjectByType<WorldManager>();

            // 주변 1청크 항상 로드되도록 뷰어 등록
            worldManager?.RegisterViewer(transform, 1);
        }

        void OnDestroy()
        {
            worldManager?.UnregisterViewer(transform);
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
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
        // 타겟 선택
        // =====================================================================

        EntityMonoBehaviour SelectTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, defenseLayer);

            var candidates = new List<EntityMonoBehaviour>();
            foreach (var col in hits)
            {
                var e = col.GetComponent<EntityMonoBehaviour>();
                if (e != null && e.IsAlive)
                    candidates.Add(e);
            }

            if (candidates.Count == 0) return null;

            return targetPriority switch
            {
                TargetPriority.LowestHp       => candidates.OrderBy(e => e.CurrentHp).First(),
                TargetPriority.HighestPriority => candidates.OrderByDescending(e => e.priorityScore).First(),
                _                              => candidates.OrderBy(e => (transform.position - e.transform.position).sqrMagnitude).First(),
            };
        }

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

            // 전방 Voxel 감지
            if (IsVoxelBlocking(dir, out Vector3 blockPoint))
            {
                // Voxel 파기 — 매 프레임 조금씩 깎음
                if (worldManager != null && digStrength > 0f)
                    worldManager.ModifyTerrain(blockPoint, digRadius, digStrength, VoxelType.Air);
                // 이번 프레임은 이동하지 않고 파기에 집중
            }
            else
            {
                _cc.Move(dir * moveSpeed * Time.deltaTime);
            }

            // 이동 방향으로 회전
            transform.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>
        /// 이동 방향으로 Raycast 후 Voxel(MeshCollider)이 막고 있으면 true.
        /// <paramref name="blockPoint"/>: 파기 목표점 (Voxel 내부 약간).
        /// </summary>
        bool IsVoxelBlocking(Vector3 dir, out Vector3 blockPoint)
        {
            blockPoint = Vector3.zero;

            float    checkDist = _cc.radius + digDetectDist;
            Vector3  origin    = transform.position + _cc.center;

            if (!Physics.Raycast(origin, dir, out RaycastHit hit, checkDist,
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
            if (Time.time - _lastAttackTime < attackCooldown) return;

            target.TakeDamage(attackDamage);
            _lastAttackTime = Time.time;
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
            Gizmos.DrawRay(transform.position, transform.forward * (_cc != null ? _cc.radius + digDetectDist : digDetectDist));
        }
    }
}
