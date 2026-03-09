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
        public static readonly int TypeCount = System.Enum.GetValues(typeof(VoxelType)).Length;

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
                //                                                   hardness  dropThreshold
                // [0] Air - 빈 공간, 파기 불가
                new VoxelProperty(VoxelType.Air,            "공기",         0f,   0f),

                // [1] Protein - 기본 재료 (흔함)
                new VoxelProperty(VoxelType.Protein,        "단백질",       1f,   500f),

                // [2] Iron - 구조 보강재 (보통)
                new VoxelProperty(VoxelType.Iron,           "철분",         3f,   200f),

                // [3] Calcium - 골격 재료 (희귀)
                new VoxelProperty(VoxelType.Calcium,        "칼슘",         7f,   100f),

                // [4] GeneticEssence - 희귀 핵산 (매우 희귀)
                new VoxelProperty(VoxelType.GeneticEssence, "유전자 정수",  20f,  50f),

                // [5] Lipid - 지방 조직 (흔함)
                new VoxelProperty(VoxelType.Lipid,          "지방",         1.5f, 400f),

                // [6] Marrow - 골수, Calcium 내부에서만 생성 (희귀)
                new VoxelProperty(VoxelType.Marrow,         "골수",         20f,   50f),

                // [7] Wall - 생체막, 맵 경계. hardness=0 → 파괴 불가
                new VoxelProperty(VoxelType.Wall,            "생체막",       0f,    0f),
            };
        }

        public static VoxelProperty GetProperty(VoxelType type)
        {
            return Properties[(int)type];
        }

        public static float GetHardness(VoxelType type)      => Properties[(int)type].hardness;
        public static float GetDropThreshold(VoxelType type) => Properties[(int)type].dropThreshold;
    }
}
