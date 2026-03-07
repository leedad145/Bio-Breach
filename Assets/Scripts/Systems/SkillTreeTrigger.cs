// =============================================================================
// SkillTreeTrigger.cs - WaitingRoom의 스킬 트리 상호작용 오브젝트
//
// 근처에서 F키 → 개인 스킬 트리 패널 열림/닫힘.
// 스킬 효과는 본인에게만 적용되며 다른 플레이어와 공유되지 않는다.
// =============================================================================
using UnityEngine;
using Unity.Netcode;

namespace BioBreach.Systems
{
    public class SkillTreeTrigger : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("상호작용")]
        [Tooltip("패널을 열 수 있는 거리")]
        [SerializeField] float   _openRadius  = 4f;
        [Tooltip("상호작용 키 (기본 F)")]
        [SerializeField] KeyCode _interactKey = KeyCode.F;

        [Header("프롬프트")]
        [SerializeField] GameObject _prompt;   // "F: 스킬 트리" 월드 오브젝트 (선택)

        // =====================================================================
        // 패널 레이아웃 상수
        // =====================================================================

        const int PanelW  = 480;
        const int TitleH  = 30;
        const int Padding = 16;
        const int NodeW   = 128;
        const int NodeH   = 78;
        const int ColSp   = 160;   // 열 간격 (node center-to-center)
        const int RowSp   = 108;   // 행 간격 (node center-to-center)
        const int Cols    = 3;
        const int Rows    = 3;
        // 패널 높이 = TitleH + Padding + (Rows-1)*RowSp + NodeH + Padding
        static readonly int PanelH = TitleH + Padding + (Rows - 1) * RowSp + NodeH + Padding;

        // =====================================================================
        // 내부 상태
        // =====================================================================

        bool        _isOpen;
        bool        _wasNearby;
        Transform   _localPlayer;
        GameObject  _localPlayerObj;

        // GUIStyle 캐시
        GUIStyle _sTitle, _sNodeName, _sNodeDesc, _sNodeCost, _sBtn;
        bool     _stylesReady;

        // =====================================================================
        // 초기화
        // =====================================================================

        void Start()
        {
            PlayerSkillData.EnsureExists();
            if (_prompt) _prompt.SetActive(false);
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            // 로컬 플레이어 캐시
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

            // 프롬프트 오브젝트 토글
            if (_prompt && nearby != _wasNearby)
                _prompt.SetActive(nearby && !_isOpen);
            _wasNearby = nearby;

            // 열기
            if (nearby && !_isOpen && Input.GetKeyDown(_interactKey))
            {
                Open();
                return;
            }
            // 닫기
            if (_isOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(_interactKey)))
                Close();
        }

        void Open()
        {
            _isOpen = true;
            if (_prompt) _prompt.SetActive(false);
            _localPlayerObj?.SendMessage("SetUIBlocked", true,  SendMessageOptions.DontRequireReceiver);
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
        // OnGUI — 스킬 트리 패널
        // =====================================================================

        void OnGUI()
        {
            if (!_isOpen) return;
            EnsureStyles();

            var data = PlayerSkillData.Instance;
            if (data == null) return;

            float px = (Screen.width  - PanelW) * 0.5f;
            float py = (Screen.height - PanelH) * 0.5f;

            // ── 배경 그림자 ─────────────────────────────────────────────────
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(px + 4, py + 4, PanelW, PanelH), Texture2D.whiteTexture);

            // ── 패널 배경 ───────────────────────────────────────────────────
            GUI.color = new Color(0.06f, 0.06f, 0.09f, 0.97f);
            GUI.DrawTexture(new Rect(px, py, PanelW, PanelH), Texture2D.whiteTexture);

            GUI.color = new Color(0.30f, 0.30f, 0.38f);
            DrawBorder(new Rect(px, py, PanelW, PanelH), 1);

            // ── 타이틀 바 ───────────────────────────────────────────────────
            GUI.color = new Color(0.10f, 0.10f, 0.15f);
            GUI.DrawTexture(new Rect(px, py, PanelW, TitleH), Texture2D.whiteTexture);
            GUI.color = new Color(0.30f, 0.30f, 0.38f);
            DrawBorderBottom(new Rect(px, py, PanelW, TitleH), 1);

            GUI.color = Color.white;
            GUI.Label(new Rect(px + 10, py + 6, 160, 20), "<b>스킬 트리</b>", _sTitle);

            GUI.color = new Color(0.9f, 0.8f, 0.3f);
            GUI.Label(new Rect(px + 170, py + 6, 160, 20),
                      $"스킬 포인트: {data.SkillPoints}", _sTitle);

            // 닫기 버튼
            Rect closeBtn = new Rect(px + PanelW - 26, py + 5, 20, 18);
            GUI.color = new Color(0.7f, 0.2f, 0.2f, 0.9f);
            GUI.DrawTexture(closeBtn, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (GUI.Button(closeBtn, "✕", _sTitle)) Close();

            // ── 열 헤더 ─────────────────────────────────────────────────────
            string[] headers = { "이동속도", "점프력", "체  력" };
            Color[]  hColors = {
                new Color(0.4f, 0.8f, 1.0f),
                new Color(0.6f, 1.0f, 0.6f),
                new Color(1.0f, 0.6f, 0.5f),
            };
            for (int c = 0; c < Cols; c++)
            {
                float cx = px + Padding + NodeW * 0.5f + c * ColSp;
                GUI.color = hColors[c];
                GUI.Label(new Rect(cx - 50, py + TitleH + 2, 100, 14), headers[c], _sNodeDesc);
            }

            // ── 연결선 (prerequisite → child) ───────────────────────────────
            GUI.color = new Color(0.45f, 0.45f, 0.55f, 0.8f);
            for (int c = 0; c < Cols; c++)
            {
                float cx = px + Padding + NodeW * 0.5f + c * ColSp;
                for (int r = 1; r < Rows; r++)
                {
                    float parentBottom = py + TitleH + Padding + NodeH * 0.5f + (r - 1) * RowSp + NodeH * 0.5f;
                    float childTop     = py + TitleH + Padding + NodeH * 0.5f + r       * RowSp - NodeH * 0.5f;
                    float lineLen      = childTop - parentBottom;
                    if (lineLen > 0f)
                        GUI.DrawTexture(new Rect(cx - 1, parentBottom, 2, lineLen), Texture2D.whiteTexture);
                }
            }

            // ── 노드 ────────────────────────────────────────────────────────
            GUI.color = Color.white;
            foreach (var node in PlayerSkillData.AllNodes)
                DrawNode(node, data, px, py);
        }

        // =====================================================================
        // 노드 그리기
        // =====================================================================

        void DrawNode(SkillNode node, PlayerSkillData data, float px, float py)
        {
            float cx = px + Padding + NodeW * 0.5f + node.treeColumn * ColSp;
            float cy = py + TitleH + Padding + NodeH * 0.5f + node.treeRow * RowSp;
            Rect  r  = new Rect(cx - NodeW * 0.5f, cy - NodeH * 0.5f, NodeW, NodeH);

            bool unlocked  = data.IsUnlocked(node.id);
            bool canUnlock = data.CanUnlock(node);

            // 배경색
            if (unlocked)
                GUI.color = new Color(0.12f, 0.38f, 0.18f, 0.95f);
            else if (canUnlock)
                GUI.color = new Color(0.38f, 0.32f, 0.06f, 0.95f);
            else
                GUI.color = new Color(0.14f, 0.14f, 0.18f, 0.95f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            // 테두리
            GUI.color = unlocked  ? new Color(0.3f, 0.8f, 0.4f) :
                        canUnlock ? new Color(0.9f, 0.75f, 0.1f) :
                                    new Color(0.25f, 0.25f, 0.30f);
            DrawBorder(r, 1);

            // 이름
            GUI.color = unlocked ? Color.white : canUnlock ? new Color(1f, 0.95f, 0.6f) : new Color(0.55f, 0.55f, 0.60f);
            GUI.Label(new Rect(r.x + 4, r.y + 4, r.width - 8, 18), node.displayName, _sNodeName);

            // 설명
            GUI.color = unlocked ? new Color(0.6f, 1f, 0.65f) : new Color(0.55f, 0.55f, 0.60f);
            GUI.Label(new Rect(r.x + 4, r.y + 22, r.width - 8, 16), node.description, _sNodeDesc);

            // 비용
            GUI.color = data.SkillPoints >= node.cost ? new Color(0.9f, 0.8f, 0.3f) : new Color(0.7f, 0.35f, 0.35f);
            string costText = unlocked ? "✓ 해제됨" : $"비용: {node.cost}pt";
            GUI.Label(new Rect(r.x + 4, r.y + 40, r.width - 8, 16), costText, _sNodeCost);

            // 선행 스킬 잠김 표시
            if (!unlocked && !string.IsNullOrEmpty(node.prerequisiteId) && !data.IsUnlocked(node.prerequisiteId))
            {
                GUI.color = new Color(0.5f, 0.35f, 0.35f);
                GUI.Label(new Rect(r.x + 4, r.y + 56, r.width - 8, 16), "선행 스킬 필요", _sNodeDesc);
            }

            // 해제 버튼
            if (canUnlock)
            {
                Rect btn = new Rect(r.x + 4, r.y + 56, r.width - 8, 18);
                GUI.color = Color.white;
                if (GUI.Button(btn, "해제", _sBtn))
                    data.TryUnlock(node);
            }

            GUI.color = Color.white;
        }

        // =====================================================================
        // 헬퍼
        // =====================================================================

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _sTitle = new GUIStyle(GUI.skin.label)
                { fontSize = 13, fontStyle = FontStyle.Bold, richText = true };
            _sTitle.normal.textColor = Color.white;

            _sNodeName = new GUIStyle(GUI.skin.label)
                { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _sNodeName.normal.textColor = Color.white;

            _sNodeDesc = new GUIStyle(GUI.skin.label)
                { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            _sNodeDesc.normal.textColor = Color.white;

            _sNodeCost = new GUIStyle(GUI.skin.label)
                { fontSize = 10, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _sNodeCost.normal.textColor = Color.white;

            _sBtn = new GUIStyle(GUI.skin.button)
                { fontSize = 10, fontStyle = FontStyle.Bold };
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

        // =====================================================================
        // 기즈모
        // =====================================================================

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _openRadius);
        }
    }
}
