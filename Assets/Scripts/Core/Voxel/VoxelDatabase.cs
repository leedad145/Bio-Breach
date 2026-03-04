// =============================================================================
// VoxelDatabase.cs - 생체 조직 복셀 타입의 속성을 관리 (바이오-브리치)
// Model 레이어 — UnityEngine 의존성 없음
// =============================================================================

namespace BioBreach.Core.Voxel
{
    /// <summary>
    /// 생체 조직 복셀 속성 데이터베이스.
    /// (int)VoxelType 값을 인덱스로 사용하므로 배열 순서는 enum 순서와 반드시 일치해야 함.
    /// 색상(Color)은 Core 레이어의 VoxelColorMap에서 관리.
    /// </summary>
    public static class VoxelDatabase
    {
        private static VoxelProperty[] _properties;

        public static VoxelProperty[] Properties
        {
            get
            {
                if (_properties == null) Initialize();
                return _properties;
            }
        }

        private static void Initialize()
        {
            _properties = new VoxelProperty[]
            {
                // [0] Air - 빈 공간 (잠식된 영역), 내구도 0 = 파기 불가
                new VoxelProperty(VoxelType.Air,            "공기",         0f),

                // [1] Protein - 기본 근육/결합 조직, 건설 재료 (분해 시 단백질 자원 획득)
                new VoxelProperty(VoxelType.Protein,        "단백질",       1f),

                // [2] Iron - 혈관/혈액 조직, 구조 보강재 (분해 시 철분 자원 획득)
                new VoxelProperty(VoxelType.Iron,           "철분",         3f),

                // [3] Calcium - 골격 조직, 기본 능력·골격 강화 (분해 시 칼슘 자원 획득)
                new VoxelProperty(VoxelType.Calcium,        "칼슘",         7f),

                // [4] GeneticEssence - 희귀 핵산 물질, 특수 기술 재료 (분해 시 유전자 정수 자원 획득)
                new VoxelProperty(VoxelType.GeneticEssence, "유전자 정수",  20f),
            };
        }

        public static VoxelProperty GetProperty(VoxelType type)
        {
            return Properties[(int)type];
        }

        public static float GetHardness(VoxelType type)
        {
            return Properties[(int)type].hardness;
        }
    }
}
