// =============================================================================
// ITerrainInteractor.cs - 복셀 지형 조작 (ISP: 지형만 담당)
// =============================================================================
using UnityEngine;
using BioBreach.Core.Voxel;

namespace BioBreach.Engine.Item
{
    /// <summary>복셀 조회 및 편집. VoxelBlockItem / UniversalMiner 등이 사용.</summary>
    public interface ITerrainInteractor
    {
        VoxelType GetVoxelTypeAt(Vector3 worldPos);
        float[]   ModifyTerrain(Vector3 pos, float radius, float strength, VoxelType type);
    }
}
