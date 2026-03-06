// =============================================================================
// MeleeWeapon.cs - 플레이어 근접 무기
// =============================================================================
using UnityEngine;
using BioBreach.Engine.Entity;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    public class MeleeWeapon : ItemBase
    {
        public LayerMask enemyLayer;

        public float attackDamage = 25f;
        public float attackReach  = 3f;
        public float attackRadius = 1.5f;

        public override ActionResult Action1(IPlayerContext ctx)
        {
            if (!ctx.PrimaryDown) return ActionResult.None;
            Vector3 center = ctx.AttackOrigin + ctx.AttackDirection * attackReach;
            foreach (var col in Physics.OverlapSphere(center, attackRadius, enemyLayer))
            {
                var entity = col.GetComponent<EntityMonoBehaviour>();
                if (entity != null && entity.IsAlive)
                    entity.TakeDamage(attackDamage);
            }
            return ActionResult.Done();
        }

        public override ActionResult Action2(IPlayerContext ctx) => ActionResult.None;
    }
}
