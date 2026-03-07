// =============================================================================
// EquipSlot.cs - 장착 슬롯 열거형 (5부위)
// =============================================================================
namespace BioBreach.Engine.Item
{
    public enum EquipSlot
    {
        Head  = 0,   // 머리
        Chest = 1,   // 상체
        Hands = 2,   // 손
        Legs  = 3,   // 하체
        Feet  = 4,   // 발
    }

    public static class EquipSlotExtensions
    {
        public static string DisplayName(this EquipSlot slot) => slot switch
        {
            EquipSlot.Head  => "머리",
            EquipSlot.Chest => "상체",
            EquipSlot.Hands => "손",
            EquipSlot.Legs  => "하체",
            EquipSlot.Feet  => "발",
            _               => slot.ToString()
        };
    }
}
