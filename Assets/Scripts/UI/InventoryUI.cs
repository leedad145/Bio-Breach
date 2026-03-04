// =============================================================================
// InventoryUI.cs - 디아블로2 스타일 그리드 인벤토리 UI (수정판)
// =============================================================================
// 핵심 변경:
//   - GUI.Window 제거 → 모든 요소를 스크린 좌표 단일 컨텍스트에서 그림
//     (GUI.Window 내부는 로컬 좌표계라 핫바와 드래그 드롭 좌표가 안 맞음)
//   - 인벤토리 열릴 때 PlayerController.UIBlocked = true → 플레이어 입력 차단
//   - 드래그: MouseDown으로 집고, 매 프레임 마우스 위치 추적, MouseUp으로 놓기
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using BioBreach.Core.Item;
using BioBreach.Engine.Inventory;
using BioBreach.Controller.Player;

namespace MarchingCubesProject.Player
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
        private bool             _isOpen = false;

        // 드래그
        private ItemInstance _dragging        = null;
        private bool         _draggingRotated = false;
        private bool         _dragFromHotbar  = false;
        private int          _dragHotbarSlot  = -1;

        // 마우스 (스크린 좌표)
        private Vector2 _mousePos;

        // 툴팁
        private ItemInstance _tooltipItem = null;
        private Vector2      _tooltipPos;

        // 창 이동용
        private bool    _windowDragging = false;
        private Vector2 _windowDragOffset;

        // 레이아웃 캐시
        private int _gridStartX, _gridStartY;   // 그리드 좌상단 스크린 좌표
        private int _step;                       // cellSize + cellPadding

        // GUIStyle 캐시
        private GUIStyle _sLabel, _sCount, _sTooltip, _sTitle;
        private bool     _stylesReady = false;

        private const int TitleH  = 28;
        private const int Padding  = 10;

        // =====================================================================
        // 초기화
        // =====================================================================

        void Awake()
        {
            _inventory  = GetComponent<PlayerInventory>();
            _controller = GetComponent<PlayerController>();
        }

        void Start() => RecalcLayout();

        void RecalcLayout()
        {
            _step       = cellSize + cellPadding;
            _gridStartX = windowX + Padding;
            _gridStartY = windowY + TitleH + Padding;
        }

        int WindowW => _inventory.gridColumns * _step + Padding * 2;
        int WindowH => TitleH + _inventory.gridRows * _step + Padding * 2;

        // =====================================================================
        // Update — 키 입력 & 드래그 추적
        // =====================================================================

        void Update()
        {
            // OnGUI의 Event는 Update보다 늦게 오므로
            // 마우스 위치는 Input에서 직접 받아 좌표 반전 (GUI는 Y 반전 없음)
            _mousePos = new Vector2(Input.mousePosition.x,
                                    Screen.height - Input.mousePosition.y);

            if (Input.GetKeyDown(KeyCode.I))
                ToggleInventory();

            if (!_isOpen) return;

            if (_dragging != null)
            {
                if (Input.GetKeyDown(KeyCode.R))
                    _draggingRotated = !_draggingRotated;

                if (Input.GetKeyDown(KeyCode.Escape))
                    CancelDrag();
            }
        }

        void ToggleInventory()
        {
            _isOpen = !_isOpen;

            if (_isOpen)
            {
                RecalcLayout();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
            else
            {
                CancelDrag();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }

            // ★ PlayerController 입력 차단
            if (_controller != null)
                _controller.UIBlocked = _isOpen;
        }

        // =====================================================================
        // OnGUI — 단일 좌표 공간에서 모두 렌더링
        // =====================================================================

        void OnGUI()
        {
            EnsureStyles();
            _tooltipItem = null;

            // 핫바는 항상 표시
            DrawHotbar();

            if (!_isOpen) return;

            // 창 이동 처리
            HandleWindowDrag();

            // 창 배경
            DrawWindow();

            // 그리드
            DrawGrid();

            // 드래그 고스트 (최상단)
            if (_dragging != null)
                DrawDragGhost();

            // 툴팁 (최상단)
            if (_tooltipItem != null && _dragging == null)
                DrawTooltip(_tooltipItem, _tooltipPos);
        }

        // =====================================================================
        // 창 배경
        // =====================================================================

        void DrawWindow()
        {
            Rect win = new Rect(windowX, windowY, WindowW, WindowH);

            // 그림자
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(win.x + 4, win.y + 4, win.width, win.height), Texture2D.whiteTexture);

            // 배경
            GUI.color = colWindowBg;
            GUI.DrawTexture(win, Texture2D.whiteTexture);

            // 테두리
            GUI.color = colWindowBorder;
            DrawBorder(win, 1);

            // 타이틀 바
            GUI.color = colTitleBar;
            GUI.DrawTexture(new Rect(win.x, win.y, win.width, TitleH), Texture2D.whiteTexture);
            GUI.color = colWindowBorder;
            DrawBorderBottom(new Rect(win.x, win.y, win.width, TitleH), 1);

            GUI.color = Color.white;
            GUI.Label(new Rect(win.x + 10, win.y + 6, win.width - 60, 20),
                      "<b>인벤토리</b>", _sTitle);

            // 닫기 버튼
            Rect closeBtn = new Rect(win.xMax - 26, win.y + 5, 20, 18);
            GUI.color = new Color(0.7f, 0.2f, 0.2f, 0.9f);
            GUI.DrawTexture(closeBtn, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (GUI.Button(closeBtn, "✕", _sTitle))
                ToggleInventory();

            // R 힌트 (드래그 중일 때만)
            if (_dragging != null && _dragging.data.gridWidth != _dragging.data.gridHeight)
            {
                GUI.color = new Color(1f, 1f, 0.4f, 0.9f);
                GUI.Label(new Rect(win.x + win.width - 110, win.y + 6, 90, 20), "R: 회전", _sTitle);
            }
            GUI.color = Color.white;
        }

        // =====================================================================
        // 그리드
        // =====================================================================

        void DrawGrid()
        {
            var grid = _inventory.Grid;
            int cols = grid.Columns;
            int rows = grid.Rows;

            // 마우스가 가리키는 셀 (스크린 좌표 기준)
            Vector2Int hoverCell = ScreenToCell(_mousePos);

            // 드래그 미리보기 셀 계산
            var  previewCells = new HashSet<Vector2Int>();
            bool previewValid = false;

            if (_dragging != null)
            {
                int pw = _draggingRotated ? _dragging.data.gridHeight : _dragging.data.gridWidth;
                int ph = _draggingRotated ? _dragging.data.gridWidth  : _dragging.data.gridHeight;
                int sx = hoverCell.x - pw / 2;
                int sy = hoverCell.y - ph / 2;

                previewValid = grid.CanPlace(pw, ph, new Vector2Int(sx, sy),
                                             ignore: _dragFromHotbar ? null : _dragging);

                for (int dx = 0; dx < pw; dx++)
                    for (int dy = 0; dy < ph; dy++)
                        previewCells.Add(new Vector2Int(sx + dx, sy + dy));
            }

            // ── 셀 배경 ──
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    Rect cr = CellRect(x, y);
                    var  pos = new Vector2Int(x, y);

                    if (previewCells.Contains(pos))
                        GUI.color = previewValid ? colCanDrop : colCannotDrop;
                    else
                        GUI.color = colEmpty;

                    GUI.DrawTexture(cr, Texture2D.whiteTexture);

                    // 안쪽 테두리 효과
                    GUI.color = new Color(1, 1, 1, 0.04f);
                    DrawBorder(cr, 1);
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
                    if (item == null || drawn.Contains(item) || item == _dragging) continue;
                    drawn.Add(item);

                    Rect ir = ItemRect(item);
                    bool hovered = ir.Contains(_mousePos) && _dragging == null;
                    bool inHotbar = IsHotbarAssigned(item);

                    GUI.color = hovered ? colHover : (inHotbar ? colSelected : colOccupied);
                    GUI.DrawTexture(ir, Texture2D.whiteTexture);

                    DrawItemContent(ir, item, false);

                    if (hovered)
                    {
                        _tooltipItem = item;
                        _tooltipPos  = _mousePos + new Vector2(14, 14);
                    }
                }
            }

            // ── 마우스 입력 ──
            HandleGridInput(cols, rows, hoverCell);
        }

        void HandleGridInput(int cols, int rows, Vector2Int hoverCell)
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                // 그리드 범위 내 클릭
                if (hoverCell.x >= 0 && hoverCell.x < cols &&
                    hoverCell.y >= 0 && hoverCell.y < rows && _dragging == null)
                {
                    var item = _inventory.Grid.GetAt(hoverCell);
                    if (item != null)
                    {
                        _dragging        = item;
                        _draggingRotated = item.isRotated;
                        _dragFromHotbar  = false;
                        _dragHotbarSlot  = -1;
                        Event.current.Use();
                    }
                }
            }

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && _dragging != null)
            {
                // 그리드 위에 놓기
                int pw = _draggingRotated ? _dragging.data.gridHeight : _dragging.data.gridWidth;
                int ph = _draggingRotated ? _dragging.data.gridWidth  : _dragging.data.gridHeight;
                int sx = hoverCell.x - pw / 2;
                int sy = hoverCell.y - ph / 2;

                bool inGrid = hoverCell.x >= 0 && hoverCell.x < cols &&
                              hoverCell.y >= 0 && hoverCell.y < rows;

                if (inGrid)
                {
                    _inventory.Grid.TryMove(_dragging, new Vector2Int(sx, sy), _draggingRotated);
                    CancelDrag();
                    Event.current.Use();
                }
                // 그리드 밖 → 핫바에서 처리하므로 여기선 그냥 둠
            }
        }

        // =====================================================================
        // 핫바
        // =====================================================================

        void DrawHotbar()
        {
            int  size    = _inventory.hotbarSize;
            int  hstep   = hotbarCellSize + 4;
            int  totalW  = hstep * size + 8;
            int  startX  = Screen.width / 2 - totalW / 2;
            int  startY  = Screen.height - hotbarCellSize - 18;

            // 배경
            Rect bg = new Rect(startX - 4, startY - 4, totalW, hotbarCellSize + 8);
            GUI.color = colHotbarBg;
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = colHotbarBorder;
            DrawBorder(bg, 1);
            GUI.color = Color.white;

            for (int i = 0; i < size; i++)
            {
                Rect sr   = new Rect(startX + i * hstep, startY, hotbarCellSize, hotbarCellSize);
                var  item = _inventory.Hotbar[i];
                bool sel  = i == _inventory.SelectedSlotIndex;

                // 슬롯 배경
                GUI.color = sel ? colSelected : (item != null ? colHotbarItem : colEmpty);
                GUI.DrawTexture(sr, Texture2D.whiteTexture);
                GUI.color = sel ? Color.yellow : colHotbarBorder;
                DrawBorder(sr, sel ? 2 : 1);
                GUI.color = Color.white;

                if (item != null)
                    DrawItemContent(sr, item, true);

                // 슬롯 번호
                GUI.color = sel ? Color.black : new Color(1, 1, 1, 0.5f);
                GUI.Label(new Rect(sr.x + 3, sr.y + 2, 18, 16), (i + 1).ToString(), _sCount);
                GUI.color = Color.white;

                if (_isOpen)
                    HandleHotbarSlotInput(i, sr, item);
            }
        }

        void HandleHotbarSlotInput(int slot, Rect sr, ItemInstance current)
        {
            var evType = Event.current.type;

            // 우클릭: 핫바 해제
            if (evType == EventType.MouseDown && Event.current.button == 1 &&
                sr.Contains(_mousePos))
            {
                _inventory.ClearHotbarSlot(slot);
                Event.current.Use();
                return;
            }

            // 좌클릭: 핫바에서 드래그 시작
            if (evType == EventType.MouseDown && Event.current.button == 0 &&
                sr.Contains(_mousePos) && current != null && _dragging == null)
            {
                _dragging        = current;
                _draggingRotated = current.isRotated;
                _dragFromHotbar  = true;
                _dragHotbarSlot  = slot;
                Event.current.Use();
                return;
            }

            // MouseUp: 핫바 슬롯에 드롭
            if (evType == EventType.MouseUp && Event.current.button == 0 &&
                sr.Contains(_mousePos) && _dragging != null)
            {
                _inventory.AssignToHotbar(slot, _dragging);
                CancelDrag();
                Event.current.Use();
            }
        }

        // =====================================================================
        // 드래그 고스트
        // =====================================================================

        void DrawDragGhost()
        {
            int pw   = _draggingRotated ? _dragging.data.gridHeight : _dragging.data.gridWidth;
            int ph   = _draggingRotated ? _dragging.data.gridWidth  : _dragging.data.gridHeight;
            float gw = pw * _step - cellPadding;
            float gh = ph * _step - cellPadding;

            Rect gr = new Rect(_mousePos.x - gw * 0.5f, _mousePos.y - gh * 0.5f, gw, gh);

            GUI.color = colDragGhost;
            GUI.DrawTexture(gr, Texture2D.whiteTexture);

            DrawItemContent(gr, _dragging, false);

            GUI.color = Color.white;
        }

        // =====================================================================
        // 툴팁
        // =====================================================================

        void DrawTooltip(ItemInstance item, Vector2 pos)
        {
            var  d   = item.data;
            int  tw  = 210;
            int  th  = 110;
            float tx = Mathf.Min(pos.x, Screen.width  - tw - 6);
            float ty = Mathf.Min(pos.y, Screen.height - th - 6);
            Rect tr  = new Rect(tx, ty, tw, th);

            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(tr.x + 3, tr.y + 3, tr.width, tr.height), Texture2D.whiteTexture);
            GUI.color = new Color(0.05f, 0.05f, 0.07f, 0.97f);
            GUI.DrawTexture(tr, Texture2D.whiteTexture);
            GUI.color = colWindowBorder;
            DrawBorder(tr, 1);
            GUI.color = Color.white;

            float ly = ty + 7;
            string cc = d.category switch
            {
                ItemCategory.VoxelBlock => "#7FC8FF",
                ItemCategory.Placeable  => "#7FE89A",
                ItemCategory.Usable     => "#FFD97F",
                _                       => "#CCCCCC"
            };
            GUI.Label(new Rect(tx + 8, ly, tw - 16, 20),
                $"<color={cc}><b>{d.itemName}</b></color>", _sTooltip); ly += 20;

            GUI.color = new Color(0.65f, 0.65f, 0.70f);
            GUI.Label(new Rect(tx + 8, ly, tw - 16, 16),
                $"{d.category}   {d.gridWidth}×{d.gridHeight} 칸", _sTooltip); ly += 18;

            if (!string.IsNullOrEmpty(d.description))
            {
                GUI.color = new Color(0.60f, 0.60f, 0.65f);
                GUI.Label(new Rect(tx + 8, ly, tw - 16, 32), d.description, _sTooltip); ly += 34;
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(tx + 8, ly, tw - 16, 18),
                $"수량  {item.count} / {d.maxStack}", _sTooltip);

            if (IsHotbarAssigned(item))
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(tx + 8, ly + 18, tw - 16, 16), "★ 핫바 등록됨", _sTooltip);
            }
            GUI.color = Color.white;
        }

        // =====================================================================
        // 아이템 컨텐츠 (아이콘 + 이름 + 수량)
        // =====================================================================

        void DrawItemContent(Rect r, ItemInstance item, bool compact)
        {
            var d = item.data;
            Rect inner = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);

            // 아이콘 or 카테고리 색 블록
            if (d.icon != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(inner, d.icon.texture);
            }
            else
            {
                GUI.color = CategoryColor(d.category);
                GUI.DrawTexture(inner, Texture2D.whiteTexture);
            }

            // 이름
            GUI.color = Color.white;
            int maxChars = compact ? 5 : Mathf.Max(6, (int)(r.width / 6));
            GUI.Label(new Rect(r.x + 3, r.y + 3, r.width - 6, 18),
                      Shorten(d.itemName, maxChars), _sLabel);

            // 수량 배지
            if (item.count > 1)
            {
                Rect badge = new Rect(r.xMax - 24, r.yMax - 18, 22, 16);
                GUI.color  = new Color(0, 0, 0, 0.65f);
                GUI.DrawTexture(badge, Texture2D.whiteTexture);
                GUI.color  = Color.yellow;
                GUI.Label(badge, item.count.ToString(), _sCount);
            }
            GUI.color = Color.white;
        }

        // =====================================================================
        // 창 이동 (타이틀 바 드래그)
        // =====================================================================

        void HandleWindowDrag()
        {
            Rect titleBar = new Rect(windowX, windowY, WindowW - 30, TitleH);

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                titleBar.Contains(_mousePos) && _dragging == null)
            {
                _windowDragging  = true;
                _windowDragOffset = _mousePos - new Vector2(windowX, windowY);
                Event.current.Use();
            }

            if (_windowDragging)
            {
                if (Event.current.type == EventType.MouseDrag ||
                    Event.current.type == EventType.MouseMove)
                {
                    Vector2 newPos = _mousePos - _windowDragOffset;
                    windowX = Mathf.Clamp((int)newPos.x, 0, Screen.width  - WindowW);
                    windowY = Mathf.Clamp((int)newPos.y, 0, Screen.height - WindowH);
                    RecalcLayout();
                }

                if (Event.current.type == EventType.MouseUp)
                    _windowDragging = false;
            }
        }

        // =====================================================================
        // 좌표 변환
        // =====================================================================

        Vector2Int ScreenToCell(Vector2 screenPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt((screenPos.x - _gridStartX) / _step),
                Mathf.FloorToInt((screenPos.y - _gridStartY) / _step));
        }

        Rect CellRect(int x, int y) =>
            new Rect(_gridStartX + x * _step, _gridStartY + y * _step, cellSize, cellSize);

        Rect ItemRect(ItemInstance item) =>
            new Rect(
                _gridStartX + item.gridPos.x * _step,
                _gridStartY + item.gridPos.y * _step,
                item.Width  * _step - cellPadding,
                item.Height * _step - cellPadding);

        // =====================================================================
        // 헬퍼
        // =====================================================================

        void CancelDrag()
        {
            _dragging       = null;
            _dragFromHotbar = false;
            _dragHotbarSlot = -1;
        }

        bool IsHotbarAssigned(ItemInstance item)
        {
            foreach (var h in _inventory.Hotbar)
                if (h == item) return true;
            return false;
        }

        Color CategoryColor(ItemCategory cat) => cat switch
        {
            ItemCategory.VoxelBlock => new Color(0.28f, 0.52f, 0.78f, 0.9f),
            ItemCategory.Placeable  => new Color(0.28f, 0.68f, 0.38f, 0.9f),
            ItemCategory.Usable     => new Color(0.78f, 0.62f, 0.18f, 0.9f),
            _                       => new Color(0.45f, 0.45f, 0.48f, 0.9f),
        };

        string Shorten(string s, int max) => s.Length <= max ? s : s[..max] + "…";

        void DrawBorder(Rect r, int t)
        {
            GUI.DrawTexture(new Rect(r.x,         r.y,          r.width, t),       Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,         r.yMax - t,   r.width, t),       Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,         r.y,          t,       r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t,  r.y,          t,       r.height), Texture2D.whiteTexture);
        }

        void DrawBorderBottom(Rect r, int t) =>
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _sLabel = new GUIStyle(GUI.skin.label)
                { fontSize = 9, wordWrap = true, richText = true };
            _sLabel.normal.textColor = Color.white;

            _sCount = new GUIStyle(GUI.skin.label)
                { fontSize = 11, alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            _sCount.normal.textColor = Color.yellow;

            _sTooltip = new GUIStyle(GUI.skin.label)
                { fontSize = 12, wordWrap = true, richText = true };
            _sTooltip.normal.textColor = Color.white;

            _sTitle = new GUIStyle(GUI.skin.label)
                { fontSize = 13, fontStyle = FontStyle.Bold, richText = true };
            _sTitle.normal.textColor = Color.white;
        }
    }
}
