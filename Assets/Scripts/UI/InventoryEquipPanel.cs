// =============================================================================
// InventoryEquipPanel.cs - 장비 슬롯 패널 (인벤토리 창 오른쪽)
// =============================================================================
using UnityEngine;
using BioBreach.Engine.Item;
using BioBreach.Engine.Inventory;

namespace BioBreach.UI
{
    public class InventoryEquipPanel
    {
        public void Draw(InventoryUIContext ctx)
        {
            int ex = ctx.WindowX + ctx.WindowW + InventoryUIContext.CraftGap;
            int ey = ctx.WindowY;
            int ew = InventoryUIContext.EquipW;
            int eh = InventoryUIContext.EquipPanelH;

            // 배경 + 테두리
            GUI.color = ctx.ColWindowBg;
            GUI.DrawTexture(new Rect(ex, ey, ew, eh), Texture2D.whiteTexture);
            GUI.color = ctx.ColWindowBorder;
            ctx.DrawBorder(new Rect(ex, ey, ew, eh), 1);

            // 타이틀 바
            GUI.color = ctx.ColTitleBar;
            GUI.DrawTexture(new Rect(ex, ey, ew, InventoryUIContext.TitleH), Texture2D.whiteTexture);
            GUI.color = ctx.ColWindowBorder;
            ctx.DrawBorderBottom(new Rect(ex, ey, ew, InventoryUIContext.TitleH), 1);
            GUI.color = Color.white;
            GUI.Label(new Rect(ex + 8, ey + 6, ew - 16, 20), "<b>장착</b>", ctx.STitle);

            // 5개 슬롯
            for (int i = 0; i < 5; i++)
            {
                var   slotType = (EquipSlot)i;
                var   equipped = ctx.Inventory.GetEquipped(slotType);
                float sy       = ey + InventoryUIContext.TitleH + InventoryUIContext.Padding
                                 + i * (InventoryUIContext.EquipSlotH + InventoryUIContext.EquipSlotGap);
                Rect  slotRect = new Rect(ex + InventoryUIContext.Padding, sy,
                                          ew - InventoryUIContext.Padding * 2,
                                          InventoryUIContext.EquipSlotH);

                bool canDrop = ctx.Dragging != null &&
                               ctx.Dragging.data is EquippableItem eq2 &&
                               (int)eq2.slot == i;

                GUI.color = canDrop ? ctx.ColCanDrop
                          : equipped != null ? ctx.ColOccupied : ctx.ColEmpty;
                GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                GUI.color = ctx.ColWindowBorder;
                ctx.DrawBorder(slotRect, 1);

                // 부위 레이블
                GUI.color = new Color(0.55f, 0.55f, 0.60f);
                GUI.Label(new Rect(slotRect.x + 3, slotRect.y + 2, slotRect.width - 6, 14),
                          slotType.DisplayName(), ctx.SLabel);

                if (equipped != null)
                {
                    Rect iconRect = new Rect(slotRect.x + 2, slotRect.y + 16,
                                            slotRect.width - 4, slotRect.height - 18);
                    ctx.DrawItemContent(iconRect, equipped, true);

                    if (iconRect.Contains(ctx.MousePos) && ctx.Dragging == null)
                    { ctx.TooltipItem = equipped; ctx.TooltipPos = ctx.MousePos + new Vector2(14, 14); }
                }

                GUI.color = Color.white;
                HandleSlotInput(ctx, slotType, slotRect, equipped);
            }
        }

        void HandleSlotInput(InventoryUIContext ctx, EquipSlot slot, Rect slotRect, ItemInstance current)
        {
            var evType = Event.current.type;

            // 우클릭: 장착 해제
            if (evType == EventType.MouseDown && Event.current.button == 1 &&
                slotRect.Contains(ctx.MousePos) && current != null)
            {
                ctx.Inventory.TryUnequip(slot);
                Event.current.Use();
                return;
            }

            // 좌클릭: 장착 아이템 드래그로 꺼내기
            if (evType == EventType.MouseDown && Event.current.button == 0 &&
                slotRect.Contains(ctx.MousePos) && current != null && ctx.Dragging == null)
            {
                ctx.Inventory.TryUnequip(slot);
                ctx.Dragging        = current;
                ctx.DraggingRotated = current.isRotated;
                ctx.DragFromHotbar  = false;
                ctx.DragHotbarSlot  = -1;
                Event.current.Use();
                return;
            }

            // MouseUp: 드래그 아이템을 슬롯에 드롭 → 장착
            if (evType == EventType.MouseUp && Event.current.button == 0 &&
                slotRect.Contains(ctx.MousePos) && ctx.Dragging != null)
            {
                if (ctx.Dragging.data is EquippableItem eq && eq.slot == slot)
                {
                    ctx.Inventory.TryEquip(ctx.Dragging);
                    ctx.CancelDrag();
                    Event.current.Use();
                }
            }
        }
    }
}
