using BioBreach.Engine.Item;

namespace BioBreach.Engine.Data.Builders
{
    public sealed class RawMaterialBuilder : IItemBuilder
    {
        public bool CanBuild(string type) => type == "RawMaterial";

        public ItemBase Build(ItemData data, ItemRepository _) => new RawMaterialItem();
    }
}
