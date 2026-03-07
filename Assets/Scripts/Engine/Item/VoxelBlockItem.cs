// =============================================================================
// VoxelBlockItem.cs - 지형 수정용 복셀 블록 아이템
// =============================================================================
using UnityEngine;
using BioBreach.Core.Voxel;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    public enum VoxelEditMode
    {
        Add,
        Remove,
        Both,
    }

    public class VoxelBlockItem : ItemBase
    {
        public VoxelType     voxelType    = VoxelType.Protein;
        public VoxelEditMode editMode     = VoxelEditMode.Both;
        public float         editRadius   = 3f;
        public float         editStrength = 0.3f;

        public override ActionResult Action1(IPlayerContext ctx)
        {
            if (!ctx.PrimaryHeld || editMode == VoxelEditMode.Add || !ctx.HasHit) return ActionResult.None;
            Vector3 digPoint = ctx.Hit.point;
            for (int i = 1; i <= 10 && ctx.GetVoxelTypeAt(digPoint) == VoxelType.Air; i++)
                digPoint = ctx.Hit.point - ctx.Hit.normal * (i * 0.3f);
            if (ctx.GetVoxelTypeAt(digPoint) == VoxelType.Air) return ActionResult.None;

            float[] dugAmounts = ctx.ModifyTerrain(digPoint, editRadius, editStrength, VoxelType.Air);
            float   dugAmount  = 0f;
            foreach (float v in dugAmounts) dugAmount += v;
            if (dugAmount <= 0f) return ActionResult.None;

            int count = ProportionalItemCount(dugAmount, editStrength);
            return count > 0 ? ActionResult.Add(this, count) : ActionResult.Done();
        }

        public override ActionResult Action2(IPlayerContext ctx)
        {
            if (!ctx.SecondaryHeld || editMode == VoxelEditMode.Remove || !ctx.HasHit) return ActionResult.None;
            Vector3 placePoint = ctx.Hit.point + ctx.Hit.normal * 0.1f;
            ctx.ModifyTerrain(placePoint, editRadius * 0.5f, -editStrength, voxelType);
            return ActionResult.Consume(1);
        }

        private static int ProportionalItemCount(float dugAmount, float strength)
        {
            float normalized = dugAmount / strength;
            int   count      = Mathf.FloorToInt(normalized);
            if (Random.value < normalized - count) count++;
            return count;
        }
    }
}
