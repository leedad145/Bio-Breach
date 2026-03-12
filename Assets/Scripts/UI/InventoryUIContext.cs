// =============================================================================
// InventoryUIContext.cs - 인벤토리 UI 공유 상태 + 레이아웃 + 스타일 + 드로우 헬퍼
//
// 모든 패널 클래스가 이 컨텍스트를 통해 상태를 읽고 쓴다.
// InventoryUI(MonoBehaviour)가 소유하며 매 프레임 갱신한다.
// =============================================================================
using UnityEngine;
using BioBreach.Engine.Inventory;
using BioBreach.Engine.Item;
using BioBreach.Controller.Player;

namespace BioBreach.UI
{
    public class InventoryUIContext
    {
        // =====================================================================
        // 레이아웃 상수
        // =====================================================================

        public const int TitleH      = 28;
        public const int Padding     = 10;
        public const int StatsPanelH = 110;
        public const int EquipW      = 106;
        public const int EquipSlotH  = 54;
        public const int EquipSlotGap = 5;
        public const int EquipPanelH = TitleH + Padding
                                     + 5 * (EquipSlotH + EquipSlotGap) - EquipSlotGap
                                     + Padding;
        public const int CraftW   = 320;
        public const int CraftGap = 8;

        // =====================================================================
        // 가변 레이아웃
        // =====================================================================

        public int WindowX, WindowY;
        public int CellSize, CellPadding;
        public int GridStartX, GridStartY;

        public int Step    => CellSize + CellPadding;
        public int WindowW => Inventory.gridColumns * Step + Padding * 2;
        public int WindowH => TitleH + Inventory.gridRows * Step + Padding * 2 + StatsPanelH;

        public void RecalcLayout()
        {
            GridStartX = WindowX + Padding;
            GridStartY = WindowY + TitleH + Padding;
        }

        // =====================================================================
        // 공유 상태
        // =====================================================================

        public Vector2      MousePos;
        public ItemInstance Dragging;
        public bool         DraggingRotated;
        public bool         DragFromHotbar;
        public int          DragHotbarSlot = -1;

        public ItemInstance TooltipItem;
        public Vector2      TooltipPos;

        // =====================================================================
        // 참조
        // =====================================================================

        public PlayerInventory  Inventory;
        public PlayerController Controller;

        // =====================================================================
        // 색상
        // =====================================================================

        public Color ColWindowBg     = new Color(0.08f, 0.08f, 0.10f, 0.96f);
        public Color ColWindowBorder = new Color(0.35f, 0.35f, 0.40f, 1.00f);
        public Color ColTitleBar     = new Color(0.12f, 0.12f, 0.16f, 1.00f);
        public Color ColEmpty        = new Color(0.13f, 0.14f, 0.16f, 0.95f);
        public Color ColOccupied     = new Color(0.22f, 0.32f, 0.44f, 0.95f);
        public Color ColHotbarItem   = new Color(0.20f, 0.30f, 0.42f, 0.95f);
        public Color ColSelected     = new Color(0.88f, 0.72f, 0.18f, 0.95f);
        public Color ColHover        = new Color(0.38f, 0.52f, 0.68f, 0.90f);
        public Color ColDragGhost    = new Color(0.25f, 0.55f, 1.00f, 0.65f);
        public Color ColHotbarBg     = new Color(0.08f, 0.08f, 0.10f, 0.92f);
        public Color ColHotbarBorder = new Color(0.30f, 0.30f, 0.35f, 1.00f);
        public Color ColCanDrop      = new Color(0.15f, 0.80f, 0.25f, 0.55f);
        public Color ColCannotDrop   = new Color(0.80f, 0.15f, 0.15f, 0.55f);

        // =====================================================================
        // GUIStyle (지연 초기화)
        // =====================================================================

        public GUIStyle SLabel, SCount, STooltip, STitle;
        private bool _stylesReady;

        public void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            SLabel = new GUIStyle(GUI.skin.label)
                { fontSize = 9, wordWrap = true, richText = true };
            SLabel.normal.textColor = Color.white;

            SCount = new GUIStyle(GUI.skin.label)
                { fontSize = 11, alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            SCount.normal.textColor = Color.yellow;

            STooltip = new GUIStyle(GUI.skin.label)
                { fontSize = 12, wordWrap = true, richText = true };
            STooltip.normal.textColor = Color.white;

            STitle = new GUIStyle(GUI.skin.label)
                { fontSize = 13, fontStyle = FontStyle.Bold, richText = true };
            STitle.normal.textColor = Color.white;
        }

        // =====================================================================
        // 상태 변경 헬퍼
        // =====================================================================

        public void CancelDrag()
        {
            Dragging       = null;
            DragFromHotbar = false;
            DragHotbarSlot = -1;
        }

        public bool IsHotbarAssigned(ItemInstance item)
        {
            foreach (var h in Inventory.Hotbar)
                if (h == item) return true;
            return false;
        }

        // =====================================================================
        // 좌표 변환
        // =====================================================================

        public Vector2Int ScreenToCell(Vector2 screenPos) => new Vector2Int(
            Mathf.FloorToInt((screenPos.x - GridStartX) / Step),
            Mathf.FloorToInt((screenPos.y - GridStartY) / Step));

        public Rect CellRect(int x, int y) =>
            new Rect(GridStartX + x * Step, GridStartY + y * Step, CellSize, CellSize);

        public Rect ItemRect(ItemInstance item) => new Rect(
            GridStartX + item.gridPos.x * Step,
            GridStartY + item.gridPos.y * Step,
            item.Width  * Step - CellPadding,
            item.Height * Step - CellPadding);

        // =====================================================================
        // 드로우 헬퍼 (모든 패널에서 공통 사용)
        // =====================================================================

        public void DrawBorder(Rect r, int t)
        {
            GUI.DrawTexture(new Rect(r.x,        r.y,        r.width, t),        Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,        r.yMax - t, r.width, t),        Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,        r.y,        t,       r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y,        t,       r.height), Texture2D.whiteTexture);
        }

        public void DrawBorderBottom(Rect r, int t) =>
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);

        public void DrawItemContent(Rect r, ItemInstance item, bool compact)
        {
            var  d     = item.data;
            Rect inner = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);

            if (d.icon != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(inner, d.icon.texture);
            }
            else
            {
                GUI.color = ItemColor(d);
                GUI.DrawTexture(inner, Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
            int maxChars = compact ? 5 : Mathf.Max(6, (int)(r.width / 6));
            GUI.Label(new Rect(r.x + 3, r.y + 3, r.width - 6, 18),
                      Shorten(d.itemName, maxChars), SLabel);

            if (item.count > 1)
            {
                Rect badge = new Rect(r.xMax - 24, r.yMax - 18, 22, 16);
                GUI.color  = new Color(0, 0, 0, 0.65f);
                GUI.DrawTexture(badge, Texture2D.whiteTexture);
                GUI.color  = Color.yellow;
                GUI.Label(badge, item.count.ToString(), SCount);
            }
            GUI.color = Color.white;
        }

        public Color ItemColor(ItemBase d)
        {
            if (d is VoxelBlockItem) return new Color(0.28f, 0.52f, 0.78f, 0.9f);
            if (d is PlaceableItem)  return new Color(0.28f, 0.68f, 0.38f, 0.9f);
            if (d is UsableItem)     return new Color(0.78f, 0.62f, 0.18f, 0.9f);
            return new Color(0.45f, 0.45f, 0.48f, 0.9f);
        }

        public string Shorten(string s, int max) => s.Length <= max ? s : s[..max] + "…";
    }
}
