using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    /// <summary>
    /// 아이템 액션 결과. ItemInstance.Dispatch가 Action 수행 후 인벤토리 변경을 처리한다.
    /// </summary>
    public readonly struct ActionResult
    {
        public readonly bool       Performed;
        public readonly ItemBase AddItem;     // null이면 추가 없음
        public readonly int        AddCount;
        public readonly int        RemoveCount; // 현재 인스턴스에서 제거할 수량

        private ActionResult(bool performed, ItemBase addItem, int addCount, int removeCount)
        {
            Performed   = performed;
            AddItem     = addItem;
            AddCount    = addCount;
            RemoveCount = removeCount;
        }

        public static ActionResult None => default;

        public static ActionResult Add(ItemBase item, int count) =>
            new ActionResult(true, item, count, 0);

        public static ActionResult Consume(int count = 1) =>
            new ActionResult(true, null, 0, count);

        public static ActionResult Done() =>
            new ActionResult(true, null, 0, 0);
    }

    /// <summary>
    /// 아이템 액션 인터페이스. ItemDataSO가 구현하며 instance를 통해 대상 슬롯을 참조한다.
    /// PlayerController → ItemInstance.Action1(ctx) → data.Action1(ctx, instance) 순으로 호출된다.
    /// </summary>
    public interface IItem
    {
        ActionResult Action1(IPlayerContext ctx);
        ActionResult Action2(IPlayerContext ctx);
    }
}
