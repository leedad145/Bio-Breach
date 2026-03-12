using BioBreach.Engine.Item;

namespace BioBreach.Engine.Data.Builders
{
    public sealed class UniversalMinerBuilder : IItemBuilder
    {
        public bool CanBuild(string type) => type == "UniversalMiner";

        public ItemBase Build(ItemData data, ItemRepository repository)
        {
            var item = new UniversalMiner();
            item.editRadius   = data.editRadius;
            item.editStrength = data.editStrength;
            // voxelDrops는 PlayerController.voxelDropIds 에서 중앙 관리된다.
            return item;
        }
    }
}
