// =============================================================================
// MeleeWeaponSO.cs - 플레이어 근접 무기
// =============================================================================

using UnityEngine;
using BioBreach.Engine.Entity;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    [CreateAssetMenu(menuName = "MarchingCubes/MeleeWeapon", fileName = "NewMeleeWeapon")]
    public class MeleeWeaponSO : ItemDataSO
    {
        [Header("근접 공격")]
        public float attackDamage = 25f;
        public float attackReach  = 3f;
        public float attackRadius = 1.5f;
        public LayerMask enemyLayer;

        public override void BindToPlayer(ItemInstance instance, IPlayerContext ctx)
        {
            instance.SetActions(
                a1: () =>
                {
                    if (!ctx.PrimaryDown) return false;
                    Vector3 center = ctx.AttackOrigin + ctx.AttackDirection * attackReach;
                    foreach (var col in Physics.OverlapSphere(center, attackRadius, enemyLayer))
                    {
                        var entity = col.GetComponent<EntityMonoBehaviour>();
                        if (entity != null && entity.IsAlive)
                            entity.TakeDamage(attackDamage);
                    }
                    return true;
                },
                a2: () => false
            );
        }
    }
}
