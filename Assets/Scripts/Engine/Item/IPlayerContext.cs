// =============================================================================
// IPlayerContext.cs - 아이템이 PlayerController에 접근하는 통합 컨텍스트
// =============================================================================
// ISP 준수: 각 책임은 아래 다섯 개 하위 인터페이스로 분리됨.
//   IInputProvider      — 마우스 버튼 상태
//   IRaycastProvider    — 레이캐스트 / 공격 원점
//   ITerrainInteractor  — 복셀 지형 조회 · 편집
//   IPlacementContext   — 오브젝트 배치
//   IInventoryContext   — 인벤토리 · 장착 · 스탯
//
// 기존 코드는 IPlayerContext 를 그대로 사용하면 됨.
// 새 아이템 클래스는 필요한 하위 인터페이스만 매개변수로 받을 수 있음.
// =============================================================================

namespace BioBreach.Engine.Item
{
    /// <summary>
    /// PlayerController가 아이템에 노출하는 통합 컨텍스트.
    /// 하위 인터페이스를 모두 구현한다.
    /// </summary>
    public interface IPlayerContext
        : IInputProvider
        , IRaycastProvider
        , ITerrainInteractor
        , IPlacementContext
        , IInventoryContext
    {
    }
}
