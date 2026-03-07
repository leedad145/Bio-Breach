using System;
using BioBreach.Engine.Item;

namespace BioBreach.Engine.Data.Builders
{
    public sealed class UsableItemBuilder : IItemBuilder
    {
        public bool CanBuild(string type) => type == "Usable";

        public ItemBase Build(ItemData data, ItemRepository _)
        {
            var item = new UsableItem();
            item.effect         = ParseEnum<UsableEffect>(data.effect, UsableEffect.None);
            item.effectValue    = data.effectValue;
            item.effectDuration = data.effectDuration;
            return item;
        }

        static T ParseEnum<T>(string value, T fallback) where T : struct, Enum
            => Enum.TryParse<T>(value, ignoreCase: true, out var r) ? r : fallback;
    }
}
