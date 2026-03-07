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

            if (data.voxelDropIds != null && data.voxelDropIds.Length > 0)
            {
                item.voxelDrops = new ItemBase[data.voxelDropIds.Length];
                for (int i = 0; i < data.voxelDropIds.Length; i++)
                {
                    var dropId = data.voxelDropIds[i];
                    if (!string.IsNullOrEmpty(dropId))
                        item.voxelDrops[i] = repository.CreateItem(dropId);
                }
            }
            return item;
        }
    }
}
