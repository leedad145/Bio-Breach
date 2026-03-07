using BioBreach.Engine.Item;

namespace BioBreach.Engine.Data.Builders
{
    public sealed class MeleeWeaponBuilder : IItemBuilder
    {
        public bool CanBuild(string type) => type == "MeleeWeapon";

        public ItemBase Build(ItemData data, ItemRepository _)
        {
            var item = new MeleeWeapon();
            item.attackDamage = data.meleeAttackDamage;
            item.attackReach  = data.meleeAttackReach;
            item.attackRadius = data.meleeAttackRadius;
            return item;
        }
    }
}
