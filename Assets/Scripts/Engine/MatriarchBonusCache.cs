// =============================================================================
// MatriarchBonusCache.cs - Systems → Controllers 단방향 의존성 우회용 캐시
//
// MatriarchGrowthData(Systems 어셈블리)가 값을 쓰고,
// MatriarchController / EnemySpawner(Controllers 어셈블리)가 읽는다.
// Engine 어셈블리는 양쪽 모두 참조하므로 중간 다리 역할을 할 수 있다.
// =============================================================================

namespace BioBreach.Engine
{
    public static class MatriarchBonusCache
    {
        public static float MaxHpBonus;         // 최대 HP 추가량
        public static float RegenBonus;         // 초당 자동 HP 회복
        public static float ArmorBonus;         // 받는 피해 감소 비율 (0~1)
        public static float SpawnDelayBonus;    // 웨이브 간격 추가 (초)
        public static float WaveRewardBonus;    // 웨이브 클리어 보상 GE 추가량

        public static event System.Action OnBonusChanged;

        public static void NotifyChanged() => OnBonusChanged?.Invoke();
    }
}
