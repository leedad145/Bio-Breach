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

        private readonly float[] _accumulation = new float[VoxelDatabase.TypeCount]; // 인덱스 = (int)VoxelType
        public float[] Accumulation => _accumulation;

        public override ActionResult Action1(IPlayerContext ctx)
        {
            if (!ctx.PrimaryHeld || !ctx.HasHit) return ActionResult.None;

            Vector3   digPoint = ctx.Hit.point;
            VoxelType dug      = VoxelType.Air;
            for (int i = 1; i <= 10; i++)
            {
                digPoint = ctx.Hit.point - ctx.Hit.normal * (i * 0.3f);
                dug = ctx.GetVoxelTypeAt(digPoint);
                if (dug != VoxelType.Air) break;
            }
            if (dug == VoxelType.Air) return ActionResult.None;

            float[] dugAmounts = ctx.ModifyTerrain(digPoint, editRadius, editStrength, VoxelType.Air);
            int     idx        = (int)dug;
            float   dugAmount  = dugAmounts[idx];
            if (dugAmount <= 0f) return ActionResult.None;

            _accumulation[idx] += dugAmount / editStrength;

            float threshold = VoxelDatabase.GetDropThreshold(dug);
            if (_accumulation[idx] < threshold) return ActionResult.Done();

            _accumulation[idx] -= threshold;

            if (voxelDrops == null || idx >= voxelDrops.Length || voxelDrops[idx] == null)
                return ActionResult.Done();

            return ActionResult.Add(voxelDrops[idx], 1);
        }

        public override ActionResult Action2(IPlayerContext ctx) => ActionResult.None;
    }
}
