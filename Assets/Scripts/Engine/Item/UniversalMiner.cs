// =============================================================================
// UniversalMiner.cs - 모든 복셀 블럭을 채굴할 수 있는 범용 채굴 도구
// =============================================================================
using UnityEngine;
using BioBreach.Core.Voxel;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    public class UniversalMiner : ItemBase
    {
        public float      editRadius   = 3f;
        public float      editStrength = 0.3f;
        public ItemBase[] voxelDrops;

        public override ActionResult Action1(IPlayerContext ctx)
        {
            if (!ctx.PrimaryHeld || !ctx.HasHit) return ActionResult.None;

            Vector3   digPoint = ctx.Hit.point - ctx.Hit.normal * 0.1f;
            VoxelType dug      = ctx.GetVoxelTypeAt(digPoint);
            if (dug == VoxelType.Air) return ActionResult.None;

            float dugAmount = ctx.ModifyTerrain(digPoint, editRadius, editStrength, VoxelType.Air);
            if (dugAmount <= 0f) return ActionResult.None;

            int idx = (int)dug;
            if (voxelDrops == null || idx >= voxelDrops.Length || voxelDrops[idx] == null)
                return ActionResult.Done();

            float normalized = dugAmount / editStrength;
            int   count      = Mathf.FloorToInt(normalized);
            if (Random.value < normalized - count) count++;
            return count > 0 ? ActionResult.Add(voxelDrops[idx], count) : ActionResult.Done();
        }

        public override ActionResult Action2(IPlayerContext ctx) => ActionResult.None;
    }
}
