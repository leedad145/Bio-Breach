// =============================================================================
// EquippableItem.cs - 플레이어가 장착할 수 있는 장비 아이템
// =============================================================================
// JSON fields: slot, hpBonus, moveSpeedBonus, jumpHeightBonus, attackDamageBonus
// =============================================================================
namespace BioBreach.Engine.Item
{
    public class EquippableItem : ItemBase
    {
        public EquipSlot slot              = EquipSlot.Chest;
        public float     hpBonus           = 0f;
        public float     moveSpeedBonus    = 0f;
        public float     jumpHeightBonus   = 0f;
        public float     attackDamageBonus = 0f;  // 향후 근접 무기 보너스 적용

        /// <summary>핫바에서 좌클릭 → 장착/해제 토글.</summary>
        public override ActionResult Action1(IPlayerContext ctx)
        {
            if (!ctx.PrimaryDown) return ActionResult.None;

            var inv      = ctx.Inventory;
            var equipped = inv.GetEquipped(slot);

            if (equipped != null && equipped.data == this)
                inv.TryUnequip(slot);
            else
                ctx.EquipSelectedItem(); // PlayerController가 선택 아이템을 장착

            return ActionResult.Done();
        }

        public override ActionResult Action2(IPlayerContext ctx) => ActionResult.None;
    }
}
