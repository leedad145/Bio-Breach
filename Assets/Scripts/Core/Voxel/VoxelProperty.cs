// =============================================================================
// VoxelProperty.cs - 복셀 속성 (이름, 내구도)
// Model 레이어 — UnityEngine 의존성 없음
// =============================================================================

namespace BioBreach.Core.Voxel
{
    /// <summary>
    /// 복셀 타입별 순수 데이터 속성.
    /// 색상(Color)은 렌더링 관심사이므로 Core 레이어의 VoxelColorMap에서 관리.
    /// </summary>
    [System.Serializable]
    public struct VoxelProperty
    {
        public VoxelType type;       // 복셀 타입
        public string displayName;   // 표시 이름
        public float hardness;       // 내구도 (높을수록 파기 어려움, 0 = 파기 불가)
        public float dropThreshold;  // 아이템 1개 획득에 필요한 누적 채굴량

        public VoxelProperty(VoxelType type, string displayName, float hardness, float dropThreshold)
        {
            this.type          = type;
            this.displayName   = displayName;
            this.hardness      = hardness;
            this.dropThreshold = dropThreshold;
        }
    }
}
