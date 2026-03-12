// =============================================================================
// InventoryUI.cs - 인벤토리 UI 코디네이터 (MonoBehaviour)
//
// 패널별 구현은 각 Panel 클래스로 분리:
//   InventoryGridPanel     — 그리드 셀 + 아이템 + 드래그&드롭
//   InventoryHotbarPanel   — 하단 핫바
//   InventoryEquipPanel    — 장착 슬롯 패널
//   InventoryCraftingPanel — 조합소 패널
//   InventoryStatsPanel    — HP바 + 스탯 분해
//
// 이 클래스가 담당하는 것:
//   - InventoryUIContext 초기화 및 매 프레임 갱신
//   - 인벤토리 열기/닫기 (I 키)
//   - 창 이동 (타이틀 바 드래그)
//   - 드래그 고스트 렌더링
//   - 툴팁 렌더링
//   - 월드 드롭 처리
// =============================================================================
using UnityEngine;
using BioBreach.Engine.Inventory;
using BioBreach.Engine.Item;
using BioBreach.Engine.Data;
using BioBreach.Controller.Player;
using BioBreach.Controller.Matriarch;

namespace BioBreach.UI
{
    [RequireComponent(typeof(PlayerInventory))]
    public class InventoryUI : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("그리드 셀")]
        public int cellSize    = 52;
        public int cellPadding = 3;

        [Header("인벤토리 창 위치 (좌상단)")]
        public int windowX = 120;
        public int windowY = 80;

        [Header("핫바")]
        public int hotbarCellSize = 58;

        [Header("색상")]
        public Color colWindowBg    = new Color(0.08f, 0.08f, 0.10f, 0.96f);
        public Color colWindowBorder= new Color(0.35f, 0.35f, 0.40f, 1.00f);
        public Color colTitleBar    = new Color(0.12f, 0.12f, 0.16f, 1.00f);
        public Color colEmpty       = new Color(0.13f, 0.14f, 0.16f, 0.95f);
        public Color colOccupied    = new Color(0.22f, 0.32f, 0.44f, 0.95f);
        public Color colHotbarItem  = new Color(0.20f, 0.30f, 0.42f, 0.95f);
        public Color colSelected    = new Color(0.88f, 0.72f, 0.18f, 0.95f);
        public Color colHover       = new Color(0.38f, 0.52f, 0.68f, 0.90f);
        public Color colDragGhost   = new Color(0.25f, 0.55f, 1.00f, 0.65f);
        public Color colHotbarBg    = new Color(0.08f, 0.08f, 0.10f, 0.92f);
        public Color colHotbarBorder= new Color(0.30f, 0.30f, 0.35f, 1.00f);
        public Color colCanDrop     = new Color(0.15f, 0.80f, 0.25f, 0.55f);
        public Color colCannotDrop  = new Color(0.80f, 0.15f, 0.15f, 0.55f);

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private PlayerInventory  _inventory;
        private PlayerController _controller;
        private bool             _isOpen;

        private bool    _windowDragging;
        private Vector2 _windowDragOffset;

        private MatriarchCraftingStation _station;

        // =====================================================================
        // 패널
        // =====================================================================

        private readonly InventoryUIContext      _ctx      = new();
        private readonly InventoryGridPanel      _grid     = new();
        private readonly InventoryHotbarPanel    _hotbar   = new();
        private readonly InventoryEquipPanel     _equip    = new();
        private readonly InventoryCraftingPanel  _crafting = new();
        private readonly InventoryStatsPanel     _stats    = new();

        // =====================================================================
        // 초기화
        // =====================================================================

        void Awake()
        {
            _inventory  = GetComponent<PlayerInventory>();
            _controller = GetComponent<PlayerController>();
        }

        void Start()
        {
            _ctx.CellSize    = cellSize;
            _ctx.CellPadding = cellPadding;
            _ctx.WindowX     = windowX;
            _ctx.WindowY     = windowY;
            _ctx.Inventory   = _inventory;
            _ctx.Controller  = _controller;
            SyncColors();
            _ctx.RecalcLayout();
        }

        void SyncColors()
        {
            _ctx.ColWindowBg     = colWindowBg;
            _ctx.ColWindowBorder = colWindowBorder;
            _ctx.ColTitleBar     = colTitleBar;
            _ctx.ColEmpty        = colEmpty;
            _ctx.ColOccupied     = colOccupied;
            _ctx.ColHotbarItem   = colHotbarItem;
            _ctx.ColSelected     = colSelected;
            _ctx.ColHover        = colHover;
            _ctx.ColDragGhost    = colDragGhost;
            _ctx.ColHotbarBg     = colHotbarBg;
            _ctx.ColHotbarBorder = colHotbarBorder;
            _ctx.ColCanDrop      = colCanDrop;
            _ctx.ColCannotDrop   = colCannotDrop;
        }

        // =====================================================================
        // Update
        // =====================================================================

        void Update()
        {
            if (_controller == null || !_controller.IsOwner) return;

            _ctx.MousePos = new Vector2(Input.mousePosition.x,
                                        Screen.height - Input.mousePosition.y);

            if (Input.GetKeyDown(KeyCode.I))
                ToggleInventory();

            if (!_isOpen) return;

            if (_ctx.Dragging != null)
            {
                if (Input.GetKeyDown(KeyCode.R))
                    _ctx.DraggingRotated = !_ctx.DraggingRotated;
                if (Input.GetKeyDown(KeyCode.Escape))
                    _ctx.CancelDrag();
            }
        }

        void ToggleInventory()
        {
            _isOpen = !_isOpen;

            if (_isOpen)
            {
                _ctx.CellSize    = cellSize;
                _ctx.CellPadding = cellPadding;
                _ctx.WindowX     = windowX;
                _ctx.WindowY     = windowY;
                SyncColors();
                _ctx.RecalcLayout();

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;

                _station = FindAnyObjectByType<MatriarchCraftingStation>();
                if (_station != null) GameDataLoader.EnsureLoaded();
                _crafting.ResetScroll();
            }
            else
            {
                _ctx.CancelDrag();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }

            if (_controller != null)
                _controller.UIBlocked = _isOpen;
        }

        // =====================================================================
        // OnGUI
        // =====================================================================

        void OnGUI()
        {
            if (_controller == null || !_controller.IsOwner) return;

            _ctx.EnsureStyles();
            _ctx.TooltipItem = null;

            // 핫바는 항상 표시
            _hotbar.Draw(_ctx, _isOpen);

            if (!_isOpen) return;

            HandleWindowDrag();
            DrawWindow();
            _grid.Draw(_ctx);
            _stats.Draw(_ctx);
            _equip.Draw(_ctx);

            if (_station != null && _station.IsLocalPlayerInRange)
                _crafting.Draw(_ctx);

            HandleWorldDrop();

            if (_ctx.Dragging != null)
                DrawDragGhost();

            if (_ctx.TooltipItem != null && _ctx.Dragging == null)
                DrawTooltip(_ctx.TooltipItem, _ctx.TooltipPos);
        }

        // =====================================================================
        // 창 배경
        // =====================================================================

        void DrawWindow()
        {
            Rect win = new Rect(_ctx.WindowX, _ctx.WindowY, _ctx.WindowW, _ctx.WindowH);

            // 그림자
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(win.x + 4, win.y + 4, win.width, win.height), Texture2D.whiteTexture);

            // 배경 + 테두리
            GUI.color = _ctx.ColWindowBg;
            GUI.DrawTexture(win, Texture2D.whiteTexture);
            GUI.color = _ctx.ColWindowBorder;
            _ctx.DrawBorder(win, 1);

            // 타이틀 바
            GUI.color = _ctx.ColTitleBar;
            GUI.DrawTexture(new Rect(win.x, win.y, win.width, InventoryUIContext.TitleH), Texture2D.whiteTexture);
            GUI.color = _ctx.ColWindowBorder;
            _ctx.DrawBorderBottom(new Rect(win.x, win.y, win.width, InventoryUIContext.TitleH), 1);

            GUI.color = Color.white;
            GUI.Label(new Rect(win.x + 10, win.y + 6, win.width - 60, 20),
                      "<b>인벤토리</b>", _ctx.STitle);

            // 닫기 버튼
            Rect closeBtn = new Rect(win.xMax - 26, win.y + 5, 20, 18);
            GUI.color = new Color(0.7f, 0.2f, 0.2f, 0.9f);
            GUI.DrawTexture(closeBtn, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (GUI.Button(closeBtn, "✕", _ctx.STitle))
                ToggleInventory();

            // R 힌트
            if (_ctx.Dragging != null &&
                _ctx.Dragging.data.gridWidth != _ctx.Dragging.data.gridHeight)
            {
                GUI.color = new Color(1f, 1f, 0.4f, 0.9f);
                GUI.Label(new Rect(win.x + win.width - 110, win.y + 6, 90, 20), "R: 회전", _ctx.STitle);
            }
            GUI.color = Color.white;
        }

        // =====================================================================
        // 창 이동
        // =====================================================================

        void HandleWindowDrag()
        {
            Rect titleBar = new Rect(_ctx.WindowX, _ctx.WindowY,
                                     _ctx.WindowW - 30, InventoryUIContext.TitleH);

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                titleBar.Contains(_ctx.MousePos) && _ctx.Dragging == null)
            {
                _windowDragging   = true;
                _windowDragOffset = _ctx.MousePos - new Vector2(_ctx.WindowX, _ctx.WindowY);
                Event.current.Use();
            }

            if (_windowDragging)
            {
                if (Event.current.type == EventType.MouseDrag ||
                    Event.current.type == EventType.MouseMove)
                {
                    Vector2 np = _ctx.MousePos - _windowDragOffset;
                    _ctx.WindowX = Mathf.Clamp((int)np.x, 0, Screen.width  - _ctx.WindowW);
                    _ctx.WindowY = Mathf.Clamp((int)np.y, 0, Screen.height - _ctx.WindowH);
                    windowX = _ctx.WindowX;
                    windowY = _ctx.WindowY;
                    _ctx.RecalcLayout();
                }

                if (Event.current.type == EventType.MouseUp)
                    _windowDragging = false;
            }
        }

        // =====================================================================
        // 월드 드롭
        // =====================================================================

        void HandleWorldDrop()
        {
            if (_ctx.Dragging == null) return;
            if (Event.current.type != EventType.MouseUp || Event.current.button != 0) return;

            string dropId    = _ctx.Dragging.data.dataId;
            int    dropCount = _ctx.Dragging.count;

            _inventory.TryRemoveItem(_ctx.Dragging, dropCount);
            _ctx.CancelDrag();
            Event.current.Use();

            if (!string.IsNullOrEmpty(dropId) && _controller != null)
                _controller.DropItemToWorld(dropId, dropCount);
        }

        // =====================================================================
        // 드래그 고스트
        // =====================================================================

        void DrawDragGhost()
        {
            int   pw = _ctx.DraggingRotated ? _ctx.Dragging.data.gridHeight : _ctx.Dragging.data.gridWidth;
            int   ph = _ctx.DraggingRotated ? _ctx.Dragging.data.gridWidth  : _ctx.Dragging.data.gridHeight;
            float gw = pw * _ctx.Step - cellPadding;
            float gh = ph * _ctx.Step - cellPadding;

            Rect gr = new Rect(_ctx.MousePos.x - gw * 0.5f, _ctx.MousePos.y - gh * 0.5f, gw, gh);
            GUI.color = _ctx.ColDragGhost;
            GUI.DrawTexture(gr, Texture2D.whiteTexture);
            _ctx.DrawItemContent(gr, _ctx.Dragging, false);
            GUI.color = Color.white;
        }

        // =====================================================================
        // 툴팁
        // =====================================================================

        void DrawTooltip(ItemInstance item, Vector2 pos)
        {
            var d  = item.data;
            int tw = 210;
            int th = d is EquippableItem eq3
                     ? 110 + (eq3.hpBonus          > 0 ? 16 : 0)
                           + (eq3.moveSpeedBonus    > 0 ? 16 : 0)
                           + (eq3.jumpHeightBonus   > 0 ? 16 : 0)
                           + (eq3.attackDamageBonus > 0 ? 16 : 0) + 50
                     : 110;

            float tx = Mathf.Min(pos.x, Screen.width  - tw - 6);
            float ty = Mathf.Min(pos.y, Screen.height - th - 6);
            Rect  tr = new Rect(tx, ty, tw, th);

            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(tr.x + 3, tr.y + 3, tr.width, tr.height), Texture2D.whiteTexture);
            GUI.color = new Color(0.05f, 0.05f, 0.07f, 0.97f);
            GUI.DrawTexture(tr, Texture2D.whiteTexture);
            GUI.color = _ctx.ColWindowBorder;
            _ctx.DrawBorder(tr, 1);
            GUI.color = Color.white;

            float ly = ty + 7f;
            string cc = d is VoxelBlockItem ? "#7FC8FF"
                      : d is PlaceableItem  ? "#7FE89A"
                      : d is UsableItem     ? "#FFD97F"
                      : "#CCCCCC";
            GUI.Label(new Rect(tx + 8, ly, tw - 16, 20),
                      $"<color={cc}><b>{d.itemName}</b></color>", _ctx.STooltip); ly += 20f;

            string typeName = d.GetType().Name.Replace("ItemSO", "").Replace("SO", "");
            GUI.color = new Color(0.65f, 0.65f, 0.70f);
            GUI.Label(new Rect(tx + 8, ly, tw - 16, 16),
                      $"{typeName}   {d.gridWidth}×{d.gridHeight} 칸", _ctx.STooltip); ly += 18f;

            if (!string.IsNullOrEmpty(d.description))
            {
                GUI.color = new Color(0.60f, 0.60f, 0.65f);
                GUI.Label(new Rect(tx + 8, ly, tw - 16, 32), d.description, _ctx.STooltip); ly += 34f;
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(tx + 8, ly, tw - 16, 18),
                      $"수량  {item.count} / {d.maxStack}", _ctx.STooltip); ly += 20f;

            if (d is EquippableItem eq)
            {
                GUI.color = new Color(0.55f, 0.85f, 1.00f);
                GUI.Label(new Rect(tx + 8, ly, tw - 16, 16),
                          $"부위: {eq.slot.DisplayName()}", _ctx.STooltip); ly += 16f;

                GUI.color = new Color(0.60f, 1.00f, 0.60f);
                if (eq.hpBonus          > 0) { GUI.Label(new Rect(tx + 8, ly, tw - 16, 16), $"HP  +{eq.hpBonus:F0}",                _ctx.STooltip); ly += 16f; }
                if (eq.moveSpeedBonus   > 0) { GUI.Label(new Rect(tx + 8, ly, tw - 16, 16), $"이동속도  +{eq.moveSpeedBonus:F1}",  _ctx.STooltip); ly += 16f; }
                if (eq.jumpHeightBonus  > 0) { GUI.Label(new Rect(tx + 8, ly, tw - 16, 16), $"점프력  +{eq.jumpHeightBonus:F1}",   _ctx.STooltip); ly += 16f; }
                if (eq.attackDamageBonus> 0) { GUI.Label(new Rect(tx + 8, ly, tw - 16, 16), $"공격력  +{eq.attackDamageBonus:F1}",_ctx.STooltip); ly += 16f; }

                bool isEquipped = _inventory.GetEquipped(eq.slot)?.data == eq;
                GUI.color = isEquipped ? Color.yellow : new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(tx + 8, ly, tw - 16, 16),
                          isEquipped ? "▶ 장착 중" : "[좌클릭] 장착  [드래그→슬롯] 장착", _ctx.STooltip);
            }

            if (_ctx.IsHotbarAssigned(item))
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(tx + 8, ly + 18f, tw - 16, 16), "★ 핫바 등록됨", _ctx.STooltip);
            }
            GUI.color = Color.white;
        }
    }
}
