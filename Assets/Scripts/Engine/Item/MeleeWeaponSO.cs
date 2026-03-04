// =============================================================================
// MeleeWeaponSO.cs - 플레이어 근접 무기
// 좌클릭 시 카메라 전방 구 범위 안의 EntityMonoBehaviour에 피해를 줌.
// Create > MarchingCubes > MeleeWeapon 으로 생성.
// =============================================================================

using UnityEngine;
using BioBreach.Engine.Entity;

namespace BioBreach.Engine.Item
{
    [CreateAssetMenu(menuName = "MarchingCubes/MeleeWeapon", fileName = "NewMeleeWeapon")]
    public class MeleeWeaponSO : ItemDataSO
    {
        [Header("근접 공격")]
        [Tooltip("공격 피해량")]
        public float attackDamage = 25f;

        [Tooltip("카메라 전방 기준 공격 도달 거리")]
        public float attackReach  = 3f;

        [Tooltip("공격 판정 구의 반경")]
        public float attackRadius = 1.5f;

        [Tooltip("피해를 입힐 레이어 (Enemy 레이어 지정)")]
        public LayerMask enemyLayer;

        // =====================================================================
        // 액션 — 좌클릭 시 전방 구 판정으로 공격
        // =====================================================================

        public override bool OnAction1(ItemActionContext ctx)
        {
            if (!ctx.PrimaryDown) return false;

            Vector3 attackCenter = ctx.AttackOrigin + ctx.AttackDirection * attackReach;
            Collider[] hits = Physics.OverlapSphere(attackCenter, attackRadius, enemyLayer);

            foreach (var col in hits)
            {
                var entity = col.GetComponent<EntityMonoBehaviour>();
                if (entity != null && entity.IsAlive)
                    entity.TakeDamage(attackDamage);
            }

            return true; // 스윙 자체가 발생했으므로 항상 true → 쿨다운 적용
        }

        public override bool OnAction2(ItemActionContext ctx) => false;
    }
}
