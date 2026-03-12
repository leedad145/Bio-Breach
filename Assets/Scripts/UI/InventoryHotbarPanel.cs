// =============================================================================
// InventoryHotbarPanel.cs - 핫바 (항상 화면 하단에 표시)
// =============================================================================
using UnityEngine;
using BioBreach.Engine.Inventory;

namespace BioBreach.UI
{
    public class InventoryHotbarPanel
    {
        public int CellSize = 58;

        public void Draw(InventoryUIContext ctx, bool inventoryOpen)
        {
            int size   = ctx.Inventory.hotbarSize;
            int hstep  = CellSize + 4;
            int totalW = hstep * size + 8;
            int startX = Screen.width  / 2 - totalW / 2;
            int startY = Screen.height - CellSize - 18;

            // 배경
            Rect bg = new Rect(startX - 4, startY - 4, totalW, CellSize + 8);
            GUI.color = ctx.ColHotbarBg;
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = ctx.ColHotbarBorder;
            ctx.DrawBorder(bg, 1);
            GUI.color = Color.white;

            for (int i = 0; i < size; i++)
            {
                Rect sr   = new Rect(startX + i * hstep, startY, CellSize, CellSize);
                var  item = ctx.Inventory.Hotbar[i];
                bool sel  = i == ctx.Inventory.SelectedSlotIndex;

                GUI.color = sel ? ctx.ColSelected : (item != null ? ctx.ColHotbarItem : ctx.ColEmpty);
                GUI.DrawTexture(sr, Texture2D.whiteTexture);
                GUI.color = sel ? Color.yellow : ctx.ColHotbarBorder;
                ctx.DrawBorder(sr, sel ? 2 : 1);
                GUI.color = Color.white;

                if (item != null) ctx.DrawItemContent(sr, item, true);

                // 슬롯 번호
                GUI.color = sel ? Color.black : new Color(1, 1, 1, 0.5f);
                GUI.Label(new Rect(sr.x + 3, sr.y + 2, 18, 16), (i + 1).ToString(), ctx.SCount);
                GUI.color = Color.white;

                if (inventoryOpen) HandleSlotInput(ctx, i, sr, item);
            }
        }

        void HandleSlotInput(InventoryUIContext ctx, int slot, Rect sr, ItemInstance current)
        {
            var evType = Event.current.type;

            // 우클릭: 핫바 해제
            if (evType == EventType.MouseDown && Event.current.button == 1 && sr.Contains(ctx.MousePos))
            {
                ctx.Inventory.ClearHotbarSlot(slot);
                Event.current.Use();
                return;
            }

            // 좌클릭: 핫바에서 드래그 시작
            if (evType == EventType.MouseDown && Event.current.button == 0 &&
                sr.Contains(ctx.MousePos) && current != null && ctx.Dragging == null)
            {
                ctx.Dragging        = current;
                ctx.DraggingRotated = current.isRotated;
                ctx.DragFromHotbar  = true;
                ctx.DragHotbarSlot  = slot;
                Event.current.Use();
                return;
            }

            // MouseUp: 핫바 슬롯에 드롭
            if (evType == EventType.MouseUp && Event.current.button == 0 &&
                sr.Contains(ctx.MousePos) && ctx.Dragging != null)
            {
                ctx.Inventory.AssignToHotbar(slot, ctx.Dragging);
                ctx.CancelDrag();
                Event.current.Use();
            }
        }
    }
}
