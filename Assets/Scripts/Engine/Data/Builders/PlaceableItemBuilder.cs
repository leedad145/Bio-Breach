using BioBreach.Engine.Item;

namespace BioBreach.Engine.Data.Builders
{
    public sealed class PlaceableItemBuilder : IItemBuilder
    {
        public bool CanBuild(string type) => type == "Placeable";

        public ItemBase Build(ItemData data, ItemRepository _)
        {
            var item = new PlaceableItem();
            item.placeDistance = data.placeDistance;
            return item;
        }
    }
}
