// =============================================================================
// InventoryGridPanel.cs - 인벤토리 그리드 (아이템 표시 + 드래그 & 드롭)
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using BioBreach.Engine.Inventory;

namespace BioBreach.UI
{
    public class InventoryGridPanel
    {
        public void Draw(InventoryUIContext ctx)
        {
            var grid = ctx.Inventory.Grid;
            int cols = grid.Columns;
            int rows = grid.Rows;

            Vector2Int hoverCell = ctx.ScreenToCell(ctx.MousePos);

            // 드래그 미리보기 셀 계산
            var  previewCells = new HashSet<Vector2Int>();
            bool previewValid = false;
            if (ctx.Dragging != null)
            {
                int pw = ctx.DraggingRotated ? ctx.Dragging.data.gridHeight : ctx.Dragging.data.gridWidth;
                int ph = ctx.DraggingRotated ? ctx.Dragging.data.gridWidth  : ctx.Dragging.data.gridHeight;
                int sx = hoverCell.x - pw / 2;
                int sy = hoverCell.y - ph / 2;

                previewValid = grid.CanPlace(pw, ph, new Vector2Int(sx, sy),
                                             ignore: ctx.DragFromHotbar ? null : ctx.Dragging);
                for (int dx = 0; dx < pw; dx++)
                    for (int dy = 0; dy < ph; dy++)
                        previewCells.Add(new Vector2Int(sx + dx, sy + dy));
            }

            // ── 셀 배경 ──
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    Rect cr  = ctx.CellRect(x, y);
                    var  pos = new Vector2Int(x, y);

                    GUI.color = previewCells.Contains(pos)
                        ? (previewValid ? ctx.ColCanDrop : ctx.ColCannotDrop)
                        : ctx.ColEmpty;
                    GUI.DrawTexture(cr, Texture2D.whiteTexture);

                    GUI.color = new Color(1, 1, 1, 0.04f);
                    ctx.DrawBorder(cr, 1);
                    GUI.color = Color.white;
                }
            }

            // ── 아이템 ──
            var drawn = new HashSet<ItemInstance>();
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    var item = grid.GetAt(x, y);
                    if (item == null || drawn.Contains(item) || item == ctx.Dragging) continue;
                    drawn.Add(item);

                    Rect ir      = ctx.ItemRect(item);
                    bool hovered = ir.Contains(ctx.MousePos) && ctx.Dragging == null;
                    bool inHotbar = ctx.IsHotbarAssigned(item);

                    GUI.color = hovered ? ctx.ColHover : (inHotbar ? ctx.ColSelected : ctx.ColOccupied);
                    GUI.DrawTexture(ir, Texture2D.whiteTexture);

                    ctx.DrawItemContent(ir, item, false);

                    if (hovered) { ctx.TooltipItem = item; ctx.TooltipPos = ctx.MousePos + new Vector2(14, 14); }
                }
            }

            HandleGridInput(ctx, cols, rows, hoverCell);
        }

        void HandleGridInput(InventoryUIContext ctx, int cols, int rows, Vector2Int hoverCell)
        {
            bool inGrid = hoverCell.x >= 0 && hoverCell.x < cols &&
                          hoverCell.y >= 0 && hoverCell.y < rows;

            // 좌클릭: 드래그 시작
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && inGrid && ctx.Dragging == null)
            {
                var item = ctx.Inventory.Grid.GetAt(hoverCell);
                if (item != null)
                {
                    ctx.Dragging        = item;
                    ctx.DraggingRotated = item.isRotated;
                    ctx.DragFromHotbar  = false;
                    ctx.DragHotbarSlot  = -1;
                    Event.current.Use();
                }
            }

            // 우클릭: 스택 분할
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1
                && inGrid && ctx.Dragging == null)
            {
                var item = ctx.Inventory.Grid.GetAt(hoverCell);
                if (item != null && item.count > 1)
                {
                    var split = ctx.Inventory.TrySplitItem(item, item.count / 2);
                    if (split != null)
                    {
                        ctx.Dragging        = split;
                        ctx.DraggingRotated = split.isRotated;
                        ctx.DragFromHotbar  = false;
                        ctx.DragHotbarSlot  = -1;
                        Event.current.Use();
                    }
                }
            }

            // 좌클릭 놓기: 합치기 or 이동
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0
                && ctx.Dragging != null && inGrid)
            {
                int pw = ctx.DraggingRotated ? ctx.Dragging.data.gridHeight : ctx.Dragging.data.gridWidth;
                int ph = ctx.DraggingRotated ? ctx.Dragging.data.gridWidth  : ctx.Dragging.data.gridHeight;
                int sx = hoverCell.x - pw / 2;
                int sy = hoverCell.y - ph / 2;

                var targetItem = ctx.Inventory.Grid.GetAt(hoverCell);
                if (targetItem != null && targetItem != ctx.Dragging
                    && targetItem.data.dataId == ctx.Dragging.data.dataId)
                    ctx.Inventory.TryMergeItem(ctx.Dragging, targetItem);
                else
                    ctx.Inventory.Grid.TryMove(ctx.Dragging, new Vector2Int(sx, sy), ctx.DraggingRotated);

                ctx.CancelDrag();
                Event.current.Use();
            }
        }
    }
}
