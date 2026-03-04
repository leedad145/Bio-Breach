// =============================================================================
// EnemyController.cs - 면역 세포 적 유닛
// 기본적으로 성체(Matriarch)를 향해 이동,
// 탐지 범위 내에 방어 유닛이 감지되면 우선순위에 따라 타겟 선정 후 이동·공격.
// 이동 중 진행 방향의 Voxel을 조금씩 파낼 수 있음.
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BioBreach.Engine.Entity;
using BioBreach.Systems;
using BioBreach.Core.Voxel;

namespace BioBreach.Controller.Enemy
{
    /// <summary>
    /// 면역 세포 적 유닛.
    /// <list type="bullet">
    ///   <item>기본: 성체(Matriarch)를 향해 이동</item>
    ///   <item>탐지 범위 내 방어 유닛 발견 시: 우선순위 타겟으로 전환하여 이동·공격</item>
    ///   <item>이동 중 진행 방향의 Voxel을 설정된 강도로 파냄</item>
    /// </list>
    /// </summary>
    public class EnemyController : EntityMonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("이동")]
        public float moveSpeed = 5f;

        [Header("탐지 & 공격")]
        [Tooltip("방어 유닛을 탐지하는 반경")]
        public float detectionRange = 15f;
        [Tooltip("공격이 발동되는 반경")]
        public float attackRange    = 2f;
        [Tooltip("방어 유닛(포탑·성체)이 있는 레이어")]
        public LayerMask defenseLayer;

        [Header("공격")]
        public float attackDamage   = 10f;
        public float attackCooldown = 1f;

        [Header("타겟 우선순위")]
        public TargetPriority targetPriority = TargetPriority.Nearest;

        [Header("기본 이동 목표")]
        [Tooltip("성체 GameObject의 Transform. 탐지 범위 내 방어 유닛이 없을 때 이 방향으로 이동.")]
        public Transform matriarchTarget;

        [Header("이동 중 Voxel 파기")]
        [Tooltip("이동 방향 앞쪽에서 파낼 구의 반경 (0이면 파기 안 함)")]
        public float digRadius   = 2f;
        [Tooltip("파기 강도 (클수록 빠르게 파냄)")]
        public float digStrength = 0.3f;
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

        float               _lastAttackTime;
        EntityMonoBehaviour _currentTarget;

        // =====================================================================
        // 초기화
        // =====================================================================

        protected override void Start()
        {
            // 스폰 시 스케일링 적용 (base.Start에서 maxHp로 RuntimeEntity를 생성하기 전에 수정)
            maxHp        *= hpMultiplier;
            attackDamage *= damageMultiplier;
            moveSpeed    *= speedMultiplier;

            base.Start();

            if (worldManager == null)
                worldManager = FindAnyObjectByType<WorldManager>();
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
        // 이동 & 공격
        // =====================================================================

        void MoveToward(Vector3 targetPos)
        {
            Vector3 diff = targetPos - transform.position;
            if (diff.sqrMagnitude <= attackRange * attackRange) return;

            Vector3 dir = diff.normalized;

            // 이동 방향 Voxel 파기
            if (worldManager != null && digRadius > 0f && digStrength > 0f)
            {
                Vector3 digCenter = transform.position + dir * (digRadius * 0.8f);
                worldManager.ModifyTerrain(digCenter, digRadius, digStrength, VoxelType.Air);
            }

            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

            Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
            if (flatDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(flatDir);
        }

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
            if (digRadius > 0f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position + transform.forward * (digRadius * 0.8f), digRadius);
            }
        }
    }
}
