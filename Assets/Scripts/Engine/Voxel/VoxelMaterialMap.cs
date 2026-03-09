// =============================================================================
// VoxelMaterialMap.cs - VoxelType → Material 매핑 (ScriptableObject)
// =============================================================================

using UnityEngine;
using BioBreach.Core.Voxel;

namespace BioBreach.Engine.Voxel
{
    /// <summary>
    /// VoxelType마다 머터리얼을 지정하는 에셋.
    /// Project 창에서 우클릭 → Create/BioBreach/VoxelMaterialMap 으로 생성.
    /// 인덱스는 (int)VoxelType 과 일치해야 함. Air(0)은 사용 안 함.
    /// </summary>
    [CreateAssetMenu(fileName = "VoxelMaterialMap", menuName = "BioBreach/VoxelMaterialMap")]
    public class VoxelMaterialMap : ScriptableObject
    {
        [Tooltip("인덱스 = (int)VoxelType\n[0] Air (미사용)\n[1] Protein\n[2] Iron\n[3] Calcium\n[4] GeneticEssence\n[5] Lipid\n[6] Marrow\n[7] Wall")]
        public Material[] materials = new Material[8];

        public Material GetMaterial(VoxelType type)
        {
            int i = (int)type;
            if (materials == null || i >= materials.Length) return null;
            return materials[i];
        }
    }
}
