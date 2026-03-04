// =============================================================================
// UniversalMinerSO.cs - 모든 복셀 블럭을 채굴할 수 있는 범용 채굴 도구
// 채굴 시 해당 VoxelType의 드롭 아이템(VoxelBlock)을 인벤토리에 추가
// =============================================================================
using UnityEngine;
using BioBreach.Core.Voxel;

namespace BioBreach.Engine.Item
{
    /// <summary>
    /// 모든 VoxelType을 채굴할 수 있는 범용 도구 ScriptableObject.
    /// Create > MarchingCubes > UniversalMiner 로 생성.
    ///
    /// voxelDrops 배열: 인덱스 = (int)VoxelType
    ///   [0] Air            = null (드롭 없음)
    ///   [1] Protein        = 단백질 VoxelBlock 아이템 SO
    ///   [2] Iron           = 철분 VoxelBlock 아이템 SO
    ///   [3] Calcium        = 칼슘 VoxelBlock 아이템 SO
    ///   [4] GeneticEssence = 유전자 정수 VoxelBlock 아이템 SO
    /// </summary>
    [CreateAssetMenu(menuName = "MarchingCubes/UniversalMiner", fileName = "NewUniversalMiner")]
    public class UniversalMinerSO : ItemDataSO
    {
        [Header("범용 채굴 - VoxelType별 드롭 아이템")]
        [Tooltip("인덱스 = (int)VoxelType. 해당 블럭을 캘 때 인벤토리에 추가될 아이템.\n" +
                 "0=Air(null), 1=Protein, 2=Iron, 3=Calcium, 4=GeneticEssence")]
        public ItemDataSO[] voxelDrops;

        /// <summary>
        /// 좌클릭 유지: VoxelType에 관계없이 모든 블럭을 채굴하고 드롭 아이템을 지급.
        /// </summary>
        public override bool OnAction1(ItemActionContext ctx)
        {
            if (!ctx.PrimaryHeld || !ctx.HasHit) return false;

            Vector3   digPoint = ctx.Hit.point - ctx.Hit.normal * 0.1f;
            VoxelType dug      = ctx.GetVoxelTypeAt(digPoint);
            if (dug == VoxelType.Air) return false;

            // 채굴 — 실제 파낸 고체 밀도 합계 반환
            float dugAmount = ctx.ModifyTerrain(digPoint, editRadius, editStrength, VoxelType.Air);
            if (dugAmount <= 0f) return false;

            // 드롭 아이템 지급: 파낸 양에 비례한 확률적 개수
            int idx = (int)dug;
            if (voxelDrops != null && idx < voxelDrops.Length && voxelDrops[idx] != null)
            {
                float normalized = dugAmount / editStrength;
                int   count      = Mathf.FloorToInt(normalized);
                float fraction   = normalized - count;
                if (Random.value < fraction) count++;
                if (count > 0) ctx.Inventory.TryAddItem(voxelDrops[idx], count);
            }

            return true;
        }
        public override bool OnAction2(ItemActionContext ctx)
        {
            return false;
        }
    }
}
