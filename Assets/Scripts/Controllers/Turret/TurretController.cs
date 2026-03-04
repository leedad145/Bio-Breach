// =============================================================================
// TurretController.cs - 방어 포탑
// 이동 없음. 탐지 범위 내 Enemy 중 우선순위에 따라 타겟 선정 후 공격.
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BioBreach.Engine.Entity;

namespace BioBreach.Controller.Turret
{
    /// <summary>
    /// 방어 포탑.
    /// <list type="bullet">
    ///   <item>이동 없음</item>
    ///   <item>탐지 범위 내 적 중 우선순위 타겟 선정</item>
    ///   <item>공격 범위 내 진입 시 공격 (쿨다운 적용)</item>
    ///   <item>barrel이 지정된 경우 포신만 타겟 방향으로 회전, 없으면 오브젝트 전체 회전</item>
    /// </list>
    /// </summary>
    public class TurretController : EntityMonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("탐지 & 공격")]
        [Tooltip("적을 탐지하는 반경")]
        public float detectionRange = 20f;
        [Tooltip("공격이 발동되는 반경 (≤ detectionRange)")]
        public float attackRange    = 18f;
        [Tooltip("Enemy가 있는 레이어")]
        public LayerMask enemyLayer;

        [Header("공격")]
        public float attackDamage   = 15f;
        public float attackCooldown = 0.8f;

        [Header("타겟 우선순위")]
        public TargetPriority targetPriority = TargetPriority.Nearest;

        [Header("포신 (선택)")]
        [Tooltip("타겟 방향으로 회전할 Transform. null이면 오브젝트 루트가 회전.")]
        public Transform barrel;

        // =====================================================================
        // 내부 변수
        // =====================================================================

        float _lastAttackTime;

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            if (!IsAlive) return;

            var target = SelectTarget();
            if (target == null) return;

            AimAt(target.transform.position);
            TryAttack(target);
        }

        // =====================================================================
        // 타겟 선택
        // =====================================================================

        EntityMonoBehaviour SelectTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, enemyLayer);

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
        // 조준 & 공격
        // =====================================================================

        void AimAt(Vector3 targetPos)
        {
            Transform pivot = barrel != null ? barrel : transform;
            Vector3   dir   = targetPos - pivot.position;
            if (dir.sqrMagnitude > 0.001f)
                pivot.rotation = Quaternion.LookRotation(dir);
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
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
