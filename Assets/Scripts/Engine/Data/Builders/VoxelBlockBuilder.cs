using System;
using BioBreach.Core.Voxel;
using BioBreach.Engine.Item;

namespace BioBreach.Engine.Data.Builders
{
    public sealed class VoxelBlockBuilder : IItemBuilder
    {
        public bool CanBuild(string type) => type == "VoxelBlock";

        public ItemBase Build(ItemData data, ItemRepository _)
        {
            var item = new VoxelBlockItem();
            item.voxelType    = ParseEnum<VoxelType>(data.voxelType,   VoxelType.Protein);
            item.editMode     = ParseEnum<VoxelEditMode>(data.editMode, VoxelEditMode.Both);
            item.editRadius   = data.editRadius;
            item.editStrength = data.editStrength;
            return item;
        }

        static T ParseEnum<T>(string value, T fallback) where T : struct, Enum
            => Enum.TryParse<T>(value, ignoreCase: true, out var r) ? r : fallback;
    }
}
