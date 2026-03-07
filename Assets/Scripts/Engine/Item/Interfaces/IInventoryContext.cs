// =============================================================================
// IInventoryContext.cs - 인벤토리 + 장착 + 스탯 컨텍스트 (ISP)
// =============================================================================
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    /// <summary>
    /// 아이템이 인벤토리를 접근하거나 플레이어 스탯을 변경할 때 사용.
    /// EquippableItem, UsableItem 등이 참조.
    /// </summary>
    public interface IInventoryContext
    {
        PlayerInventory Inventory { get; }
        void EquipSelectedItem();
        void AddMoveSpeed(float v, float duration = 0f);
        void AddJumpHeight(float v, float duration = 0f);
    }
}
