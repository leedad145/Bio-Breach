using System;
using BioBreach.Engine.Item;

namespace BioBreach.Engine.Data.Builders
{
    public sealed class EquippableItemBuilder : IItemBuilder
    {
        public bool CanBuild(string type) => type == "Equippable";

        public ItemBase Build(ItemData data, ItemRepository _)
        {
            var item = new EquippableItem();
            item.slot              = ParseEnum<EquipSlot>(data.equipSlot, EquipSlot.Chest);
            item.hpBonus           = data.hpBonus;
            item.moveSpeedBonus    = data.moveSpeedBonus;
            item.jumpHeightBonus   = data.jumpHeightBonus;
            item.attackDamageBonus = data.attackDamageBonus;
            return item;
        }

        static T ParseEnum<T>(string value, T fallback) where T : struct, Enum
            => Enum.TryParse<T>(value, ignoreCase: true, out var r) ? r : fallback;
    }
}
