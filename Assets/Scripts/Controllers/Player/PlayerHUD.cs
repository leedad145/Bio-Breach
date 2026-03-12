// =============================================================================
// PlayerHUD.cs - 디버그 / 채굴 진행도 GUI
// =============================================================================
using UnityEngine;
using Unity.Netcode;
using BioBreach.Core.Voxel;
using BioBreach.Engine.Inventory;
using BioBreach.Engine.Item;

namespace BioBreach.Controller.Player
{
    public class PlayerHUD : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("디버그")]
        public bool showDebugUI = true;

        // =====================================================================
        // 런타임 참조 (Init 으로 주입)
        // =====================================================================

        private NetworkBehaviour _owner;    // IsOwner 체크용
        private PlayerInventory  _inventory;
        private PlayerAction     _action;

        private GUIStyle _style;

        // =====================================================================
        // 초기화
        // =====================================================================

        public void Init(NetworkBehaviour owner, PlayerInventory inventory, PlayerAction action)
        {
            _owner     = owner;
            _inventory = inventory;
            _action    = action;
        }

        // =====================================================================
        // GUI
        // =====================================================================

        void OnGUI()
        {
            if (_owner == null || !_owner.IsOwner) return;
            if (_inventory == null) return;

            DrawCrosshair();

            // 채굴 HUD — 항상 표시 (맨손 or 채굴기 누적값)
            var minerData = _inventory.SelectedItem?.data as UniversalMiner;
            DrawMinerHUD(minerData?.Accumulation ?? _action.BareHandAccumulation);

            if (!showDebugUI) return;

            DrawSelectedItem();
            DrawHotbar();
            DrawHints();
        }

        void DrawCrosshair()
        {
            int cx = Screen.width / 2, cy = Screen.height / 2;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - 20, cy - 1, 40, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy - 20, 2, 40), Texture2D.whiteTexture);
        }

        void DrawSelectedItem()
        {
            int cx = Screen.width / 2, cy = Screen.height / 2;
            var sel = _inventory.SelectedItem;
            if (sel != null)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(cx + 30, cy - 10, 250, 25), $"[{sel.data.GetType().Name}] {sel.data.itemName}");
                GUI.Label(new Rect(cx + 30, cy + 10, 250, 25), $"수량: {sel.count}");
            }
            else
            {
                GUI.color = Color.cyan;
                GUI.Label(new Rect(cx + 30, cy - 10, 250, 25), "[맨손]");
            }
        }

        void DrawHotbar()
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(10, 10, 200, 20), "=== 핫바 ===");
            for (int i = 0; i < _inventory.hotbarSize; i++)
            {
                var  item  = _inventory.Hotbar[i];
                bool isSel = i == _inventory.SelectedSlotIndex;
                if (isSel)
                {
                    GUI.color = Color.yellow;
                    GUI.DrawTexture(new Rect(8, 32 + i * 24 - 2, 180, 22), Texture2D.whiteTexture);
                }
                GUI.color = isSel ? Color.black : Color.white;
                string label = item != null ? $"[{i+1}] {item.data.itemName} x{item.count}" : $"[{i+1}] -";
                GUI.Label(new Rect(12, 32 + i * 24, 160, 20), label);
                GUI.color = Color.white;
            }
        }

        void DrawHints()
        {
            GUI.color = Color.white;
            int y = Screen.height - 120;
            GUI.Label(new Rect(10, y,      300, 20), "WASD: 이동 | Space: 점프 | ESC: 커서");
            GUI.Label(new Rect(10, y + 20, 300, 20), "좌클릭: 공격/채굴  우클릭: 설치/사용");
            GUI.Label(new Rect(10, y + 40, 300, 20), "1~5: 핫바 선택 | 휠: 변경");
            GUI.Label(new Rect(10, y + 60, 300, 20), "I: 인벤토리");
        }

        void DrawMinerHUD(float[] acc)
        {
            EnsureStyle();

            string[] names = { "", "단백질", "철분", "칼슘", "유전자정수", "지방", "골수" };
            float rowH   = 15f;
            float panelW = 165f;
            float panelH = rowH + 4 * (rowH + 2) + 8f;
            float px     = Screen.width  - panelW - 12f;
            float py     = Screen.height - panelH - 120f;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(px - 4, py - 4, panelW + 8, panelH + 8), Texture2D.whiteTexture);

            GUI.color = new Color(0.7f, 0.9f, 1.0f);
            GUI.Label(new Rect(px, py, panelW, rowH), "채굴 진행도", _style);

            for (int i = 1; i <= 4; i++)
            {
                float ry        = py + rowH + (i - 1) * (rowH + 2);
                float progress  = acc[i];
                float threshold = VoxelDatabase.GetDropThreshold((VoxelType)i);
                float pct       = threshold > 0f ? Mathf.Clamp01(progress / threshold) : 0f;
                float barX      = px + 58f;
                float barW      = panelW - 62f;

                GUI.color = new Color(0.18f, 0.18f, 0.18f, 0.8f);
                GUI.DrawTexture(new Rect(barX, ry + 2, barW, rowH - 4), Texture2D.whiteTexture);
                if (pct > 0f)
                {
                    GUI.color = new Color(0.3f, 0.8f, 0.4f, 0.9f);
                    GUI.DrawTexture(new Rect(barX, ry + 2, barW * pct, rowH - 4), Texture2D.whiteTexture);
                }

                GUI.color = Color.white;
                GUI.Label(new Rect(px, ry, 58f, rowH), names[i], _style);
                GUI.Label(new Rect(barX + barW - 48f, ry, 50f, rowH),
                          $"{(int)progress}/{(int)threshold}", _style);
            }
            GUI.color = Color.white;
        }

        void EnsureStyle()
        {
            if (_style != null) return;
            _style = new GUIStyle(GUI.skin.label) { fontSize = 10 };
            _style.normal.textColor = Color.white;
        }
    }
}
