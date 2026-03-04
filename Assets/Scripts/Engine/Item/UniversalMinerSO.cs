// =============================================================================
// UniversalMinerSO.cs - 모든 복셀 블럭을 채굴할 수 있는 범용 채굴 도구
// =============================================================================
using UnityEngine;
using BioBreach.Core.Voxel;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    [CreateAssetMenu(menuName = "MarchingCubes/UniversalMiner", fileName = "NewUniversalMiner")]
    public class UniversalMinerSO : ItemDataSO
    {
        [Header("범용 채굴 - VoxelType별 드롭 아이템")]
        [Tooltip("인덱스 = (int)VoxelType. 0=Air(null), 1=Protein, 2=Iron, 3=Calcium, 4=GeneticEssence")]
        public ItemDataSO[] voxelDrops;

        public override void BindToPlayer(ItemInstance instance, IPlayerContext ctx)
        {
            instance.SetActions(
                a1: () =>
                {
                    if (!ctx.PrimaryHeld || !ctx.HasHit) return false;

                    Vector3   digPoint = ctx.Hit.point - ctx.Hit.normal * 0.1f;
                    VoxelType dug      = ctx.GetVoxelTypeAt(digPoint);
                    if (dug == VoxelType.Air) return false;

                    float dugAmount = ctx.ModifyTerrain(digPoint, editRadius, editStrength, VoxelType.Air);
                    if (dugAmount <= 0f) return false;

                    int idx = (int)dug;
                    if (voxelDrops != null && idx < voxelDrops.Length && voxelDrops[idx] != null)
                    {
                        float normalized = dugAmount / editStrength;
                        int   count      = Mathf.FloorToInt(normalized);
                        if (Random.value < normalized - count) count++;
                        if (count > 0) ctx.Inventory.TryAddItem(voxelDrops[idx], count);
                    }
                    return true;
                },
                a2: () => false
            );
        }
    }
}
