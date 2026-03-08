// =============================================================================
// MatriarchGrowthTrigger.cs - WaitingRoom의 성체 성장 트리 상호작용 오브젝트
//
// 근처에서 G키 → 성체 성장 트리 패널 열림/닫힘.
// 해제된 노드는 모든 플레이어에게 공유된다.
// raw_genetic_essence 아이템으로 노드를 해제한다.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using BioBreach.Engine.Item;
using BioBreach.Engine.Inventory;

namespace BioBreach.Systems
{
    public class MatriarchGrowthTrigger : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("상호작용")]
        [SerializeField] float   _openRadius  = 4f;
        [SerializeField] KeyCode _interactKey = KeyCode.G;

        [Header("프롬프트")]
        [SerializeField] GameObject _prompt;

        // =====================================================================
        // 패널 레이아웃 상수
        // =====================================================================

        const int PanelW  = 560;
        const int PanelH  = 444;
        const int TitleH  = 30;
        const int InfoH   = 24;   // GE 수량 표시 바
        const int HeaderH = 18;
        const int Padding = 16;
        const int NodeW   = 140;
        const int NodeH   = 90;
        const int ColSp   = 176;
        const int RowSp   = 118;

        // =====================================================================
        // 내부 상태
        // =====================================================================

        bool        _isOpen;
        bool        _wasNearby;
        Transform   _localPlayer;
        GameObject  _localPlayerObj;
        Vector2     _scrollPos;

        GUIStyle _sTitle, _sNodeName, _sNodeDesc, _sNodeCost, _sBtn;
        bool     _stylesReady;

        Dictionary<string, MatriarchNode> _nodeMap;
        IInventoryContext _inventoryCtx;

        // =====================================================================
        // 초기화
        // =====================================================================

        void Start()
        {
            if (_prompt) _prompt.SetActive(false);
        }

        void OnEnable()  => MatriarchGrowthData.OnGrowthChanged += OnGrowthChanged;
        void OnDisable() => MatriarchGrowthData.OnGrowthChanged -= OnGrowthChanged;

        void OnGrowthChanged() { /* OnGUI가 다음 프레임에 자동 갱신 */ }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            if (_localPlayer == null)
            {
                var obj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
                if (obj != null)
                {
                    _localPlayer    = obj.transform;
                    _localPlayerObj = obj.gameObject;
                }
            }
            if (_localPlayer == null) return;

            bool nearby = Vector3.Distance(transform.position, _localPlayer.position) <= _openRadius;

            if (_prompt && nearby != _wasNearby)
                _prompt.SetActive(nearby && !_isOpen);
            _wasNearby = nearby;

            if (nearby && !_isOpen && Input.GetKeyDown(_interactKey))
            {
                Open();
                return;
            }
            if (_isOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(_interactKey)))
                Close();
        }

        void Open()
        {
            _isOpen  = true;
            _nodeMap = null;
            _inventoryCtx = _localPlayerObj?.GetComponent<IInventoryContext>();
            if (_prompt) _prompt.SetActive(false);
            _localPlayerObj?.SendMessage("SetUIBlocked", true, SendMessageOptions.DontRequireReceiver);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        void Close()
        {
            _isOpen = false;
            _localPlayerObj?.SendMessage("SetUIBlocked", false, SendMessageOptions.DontRequireReceiver);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        // =====================================================================
        // OnGUI
        // =====================================================================

        void OnGUI()
        {
            if (!_isOpen) return;
            EnsureStyles();

            var growthData = MatriarchGrowthData.Instance;
            if (growthData == null)
            {
                // 게임 씬 아니면 아직 스폰 안 됨 — 안내 메시지
                float px2 = (Screen.width  - 300) * 0.5f;
                float py2 = (Screen.height - 40)  * 0.5f;
                GUI.color = Color.white;
                GUI.Label(new Rect(px2, py2, 300, 40), "성체 데이터 미연결", _sTitle);
                return;
            }

            EnsureNodeMap();

            float px = (Screen.width  - PanelW) * 0.5f;
            float py = (Screen.height - PanelH) * 0.5f;

            // 그림자
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(px + 4, py + 4, PanelW, PanelH), Texture2D.whiteTexture);

            // 배경
            GUI.color = new Color(0.05f, 0.07f, 0.06f, 0.97f);
            GUI.DrawTexture(new Rect(px, py, PanelW, PanelH), Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.45f, 0.30f);
            DrawBorder(new Rect(px, py, PanelW, PanelH), 1);

            // 타이틀 바
            GUI.color = new Color(0.08f, 0.12f, 0.10f);
            GUI.DrawTexture(new Rect(px, py, PanelW, TitleH), Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.45f, 0.30f);
            DrawBorderBottom(new Rect(px, py, PanelW, TitleH), 1);

            GUI.color = new Color(0.5f, 1f, 0.6f);
            GUI.Label(new Rect(px + 10, py + 6, 200, 20), "<b>성체 성장 트리</b>", _sTitle);

            // 닫기
            Rect closeBtn = new Rect(px + PanelW - 26, py + 5, 20, 18);
            GUI.color = new Color(0.7f, 0.2f, 0.2f, 0.9f);
            GUI.DrawTexture(closeBtn, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (GUI.Button(closeBtn, "✕", _sTitle)) Close();

            // GE 수량 바
            Rect infoRect = new Rect(px, py + TitleH, PanelW, InfoH);
            GUI.color = new Color(0.06f, 0.10f, 0.08f, 1f);
            GUI.DrawTexture(infoRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.20f, 0.38f, 0.24f);
            DrawBorderBottom(infoRect, 1);

            PlayerInventory inv = _inventoryCtx?.Inventory;
            int geCount = inv != null ? inv.GetTotalCount("raw_genetic_essence") : 0;

            GUI.color = new Color(0.5f, 1f, 0.5f);
            GUI.Label(new Rect(px + 10, py + TitleH + 4, PanelW - 20, 16),
                      $"보유 raw_genetic_essence: {geCount}개  |  공유 상태 (모든 플레이어 반영)", _sNodeDesc);

            // 스크롤 뷰
            Rect viewRect    = new Rect(px, py + TitleH + InfoH, PanelW, PanelH - TitleH - InfoH);
            Rect contentRect = GetContentRect();

            _scrollPos = GUI.BeginScrollView(viewRect, _scrollPos, contentRect);

            var nodes = MatriarchGrowthData.AllNodes;

            // 열 헤더
            string[] headers = { "HP 강화", "회복/방어", "웨이브" };
            Color[]  hColors = {
                new Color(1.0f, 0.5f, 0.4f),
                new Color(0.4f, 0.9f, 0.6f),
                new Color(0.5f, 0.8f, 1.0f),
            };
            var usedCols = new HashSet<int>(8);
            foreach (var n in nodes) usedCols.Add(n.treeColumn);
            foreach (int c in usedCols)
            {
                float cx = NodeCX(c);
                GUI.color = (c < hColors.Length) ? hColors[c] : Color.white;
                GUI.Label(new Rect(cx - 55, 2, 110, HeaderH - 2),
                          (c < headers.Length) ? headers[c] : $"열 {c}", _sNodeDesc);
            }

            // 연결선
            GUI.color = new Color(0.3f, 0.55f, 0.35f, 0.8f);
            foreach (var node in nodes)
            {
                if (node.prerequisiteIds == null || node.prerequisiteIds.Length == 0) continue;
                float childX   = NodeCX(node.treeColumn);
                float childTop = NodeCY(node.treeRow) - NodeH * 0.5f;
                foreach (var pid in node.prerequisiteIds)
                {
                    if (string.IsNullOrEmpty(pid)) continue;
                    if (!_nodeMap.TryGetValue(pid, out var prereq)) continue;
                    DrawOrthogonalLine(NodeCX(prereq.treeColumn), NodeCY(prereq.treeRow) + NodeH * 0.5f,
                                       childX, childTop);
                }
            }

            // 노드
            GUI.color = Color.white;
            foreach (var node in nodes)
                DrawNode(node, growthData, inv, geCount);

            GUI.EndScrollView();
        }

        // =====================================================================
        // 노드 그리기 (content 좌표계)
        // =====================================================================

        void DrawNode(MatriarchNode node, MatriarchGrowthData gd, PlayerInventory inv, int geCount)
        {
            float cx = NodeCX(node.treeColumn);
            float cy = NodeCY(node.treeRow);
            Rect  r  = new Rect(cx - NodeW * 0.5f, cy - NodeH * 0.5f, NodeW, NodeH);

            bool unlocked  = gd.IsUnlocked(node.id);
            bool canUnlock = gd.CanUnlock(node);
            bool hasGe     = geCount >= node.cost;

            if (unlocked)
                GUI.color = new Color(0.10f, 0.32f, 0.16f, 0.95f);
            else if (canUnlock && hasGe)
                GUI.color = new Color(0.28f, 0.38f, 0.10f, 0.95f);
            else
                GUI.color = new Color(0.10f, 0.14f, 0.12f, 0.95f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            GUI.color = unlocked         ? new Color(0.3f, 0.85f, 0.4f)  :
                        canUnlock && hasGe ? new Color(0.7f, 0.9f, 0.1f)  :
                                            new Color(0.20f, 0.28f, 0.22f);
            DrawBorder(r, 1);

            GUI.color = unlocked ? Color.white : (canUnlock ? new Color(0.9f, 1f, 0.7f) : new Color(0.45f, 0.55f, 0.48f));
            GUI.Label(new Rect(r.x + 4, r.y + 4, r.width - 8, 18), node.displayName, _sNodeName);

            GUI.color = unlocked ? new Color(0.5f, 1f, 0.6f) : new Color(0.45f, 0.55f, 0.48f);
            GUI.Label(new Rect(r.x + 4, r.y + 22, r.width - 8, 16), node.description, _sNodeDesc);

            // 비용
            GUI.color = hasGe ? new Color(0.9f, 0.8f, 0.3f) : new Color(0.7f, 0.35f, 0.35f);
            string costText = unlocked ? "✓ 해제됨" : $"비용: {node.cost} GE";
            GUI.Label(new Rect(r.x + 4, r.y + 40, r.width - 8, 16), costText, _sNodeCost);

            // 선행 스킬 미충족 경고
            if (!unlocked && node.prerequisiteIds != null)
            {
                bool anyUnmet = false;
                foreach (var pid in node.prerequisiteIds)
                    if (!string.IsNullOrEmpty(pid) && !gd.IsUnlocked(pid)) { anyUnmet = true; break; }
                if (anyUnmet)
                {
                    GUI.color = new Color(0.6f, 0.35f, 0.35f);
                    GUI.Label(new Rect(r.x + 4, r.y + 58, r.width - 8, 16), "선행 노드 필요", _sNodeDesc);
                }
            }

            // 해제 버튼
            if (canUnlock)
            {
                Rect btn = new Rect(r.x + 4, r.y + 66, r.width - 8, 18);
                GUI.color = hasGe ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                if (GUI.Button(btn, hasGe ? "해제" : "GE 부족", _sBtn) && hasGe)
                    MatriarchGrowthData.Instance?.PurchaseNodeServerRpc(node.id);
            }

            GUI.color = Color.white;
        }

        // =====================================================================
        // 헬퍼
        // =====================================================================

        static float NodeCX(int col) => Padding + NodeW * 0.5f + col * ColSp;
        static float NodeCY(int row) => HeaderH + 4 + NodeH * 0.5f + row * RowSp;

        Rect GetContentRect()
        {
            int maxCol = 2, maxRow = 2;
            foreach (var n in MatriarchGrowthData.AllNodes)
            {
                if (n.treeColumn > maxCol) maxCol = n.treeColumn;
                if (n.treeRow    > maxRow) maxRow = n.treeRow;
            }
            float w = Padding * 2 + NodeW + maxCol * ColSp;
            float h = HeaderH + 4 + NodeH + maxRow * RowSp + Padding;
            return new Rect(0, 0, Mathf.Max(w, PanelW - 16), h);
        }

        void EnsureNodeMap()
        {
            if (_nodeMap != null) return;
            _nodeMap = new Dictionary<string, MatriarchNode>();
            foreach (var n in MatriarchGrowthData.AllNodes)
                _nodeMap[n.id] = n;
        }

        void DrawOrthogonalLine(float x1, float y1, float x2, float y2)
        {
            const float T = 2f;
            if (Mathf.Abs(x1 - x2) < 2f)
            {
                float top = Mathf.Min(y1, y2);
                float len = Mathf.Abs(y2 - y1);
                if (len > 0f) GUI.DrawTexture(new Rect(x1 - T * 0.5f, top, T, len), Texture2D.whiteTexture);
                return;
            }
            float midY   = (y1 + y2) * 0.5f;
            float hLeft  = Mathf.Min(x1, x2);
            float hRight = Mathf.Max(x1, x2);
            GUI.DrawTexture(new Rect(x1 - T * 0.5f, y1,             T,              midY - y1),  Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(hLeft,          midY - T * 0.5f, hRight - hLeft, T),          Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x2 - T * 0.5f, midY,            T,              y2 - midY),  Texture2D.whiteTexture);
        }

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _sTitle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, richText = true };
            _sTitle.normal.textColor = Color.white;

            _sNodeName = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _sNodeName.normal.textColor = Color.white;

            _sNodeDesc = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            _sNodeDesc.normal.textColor = Color.white;

            _sNodeCost = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _sNodeCost.normal.textColor = Color.white;

            _sBtn = new GUIStyle(GUI.skin.button) { fontSize = 10, fontStyle = FontStyle.Bold };
        }

        void DrawBorder(Rect r, int t)
        {
            GUI.DrawTexture(new Rect(r.x,        r.y,         r.width, t),        Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,        r.yMax - t,  r.width, t),        Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,        r.y,         t,       r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y,         t,       r.height), Texture2D.whiteTexture);
        }

        void DrawBorderBottom(Rect r, int t) =>
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _openRadius);
        }
    }
}
