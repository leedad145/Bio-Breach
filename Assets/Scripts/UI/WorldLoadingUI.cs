// =============================================================================
// WorldLoadingUI.cs - 맵 청크 생성 로딩 화면
// WorldManager.OnLoadingProgress / OnWorldReady 이벤트 구독.
// 씬에 빈 GameObject를 만들고 이 컴포넌트를 붙이면 됨. Canvas 불필요.
// =============================================================================

using UnityEngine;
using BioBreach.Systems;

namespace BioBreach.UI
{
    public class WorldLoadingUI : MonoBehaviour
    {
        // =====================================================================
        // 설정
        // =====================================================================

        [Tooltip("로딩 완료 후 UI를 서서히 숨기는 데 걸리는 시간 (초)")]
        [SerializeField] float fadeOutDuration = 0.8f;

        // =====================================================================
        // 내부 상태
        // =====================================================================

        float _progress   = 0f;
        bool  _done       = false;
        float _fadeTimer  = 0f;

        // GUIStyle 캐시
        GUIStyle _sTitle, _sSub;
        bool     _stylesReady;

        // =====================================================================
        // 레이아웃 상수
        // =====================================================================

        const int PanelW   = 400;
        const int PanelH   = 110;
        const int BarH     = 18;
        const int Padding  = 16;

        // =====================================================================
        // 이벤트 구독
        // =====================================================================

        void OnEnable()
        {
            WorldManager.OnLoadingProgress += HandleProgress;
            WorldManager.OnWorldReady      += HandleReady;
        }

        void OnDisable()
        {
            WorldManager.OnLoadingProgress -= HandleProgress;
            WorldManager.OnWorldReady      -= HandleReady;
        }

        void HandleProgress(float progress) => _progress = progress;

        void HandleReady()
        {
            _progress  = 1f;
            _done      = true;
            _fadeTimer = 0f;
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            if (!_done) return;
            _fadeTimer += Time.deltaTime;
            if (_fadeTimer >= fadeOutDuration)
                gameObject.SetActive(false);
        }

        // =====================================================================
        // OnGUI
        // =====================================================================

        void OnGUI()
        {
            if (!_stylesReady) BuildStyles();

            // 알파 계산 (완료 후 페이드아웃)
            float alpha = _done
                ? Mathf.Clamp01(1f - _fadeTimer / fadeOutDuration)
                : 1f;

            if (alpha <= 0f) return;

            // ── 풀스크린 어두운 배경 ─────────────────────────────────────────
            GUI.color = new Color(0f, 0f, 0f, 0.75f * alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // ── 중앙 패널 ────────────────────────────────────────────────────
            float px = (Screen.width  - PanelW) * 0.5f;
            float py = (Screen.height - PanelH) * 0.5f;
            Rect panel = new Rect(px, py, PanelW, PanelH);

            GUI.color = new Color(0.04f, 0.06f, 0.10f, 0.95f * alpha);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);

            // 테두리
            GUI.color = new Color(0.25f, 0.55f, 0.45f, 0.8f * alpha);
            GUI.DrawTexture(new Rect(px,             py,              PanelW, 1),      Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(px,             py + PanelH - 1, PanelW, 1),      Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(px,             py,              1,      PanelH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(px + PanelW - 1, py,            1,      PanelH), Texture2D.whiteTexture);

            // ── 제목 ─────────────────────────────────────────────────────────
            GUI.color = new Color(0.55f, 1f, 0.75f, alpha);
            GUI.Label(new Rect(px + Padding, py + Padding, PanelW - Padding * 2, 22),
                      _done ? "<b>생체 조직 초기화 완료</b>" : "<b>생체 조직 초기화 중...</b>",
                      _sTitle);

            // ── 진행률 텍스트 ────────────────────────────────────────────────
            GUI.color = new Color(0.7f, 0.85f, 0.8f, alpha);
            GUI.Label(new Rect(px + Padding, py + Padding + 24, PanelW - Padding * 2, 18),
                      $"청크 생성  {_progress * 100f:F0}%",
                      _sSub);

            // ── 진행 바 배경 ─────────────────────────────────────────────────
            Rect barBg = new Rect(px + Padding, py + PanelH - BarH - Padding,
                                  PanelW - Padding * 2, BarH);
            GUI.color = new Color(0.08f, 0.12f, 0.11f, 0.9f * alpha);
            GUI.DrawTexture(barBg, Texture2D.whiteTexture);

            // ── 진행 바 채움 ─────────────────────────────────────────────────
            if (_progress > 0f)
            {
                Color fillColor = _done
                    ? new Color(0.3f, 1f, 0.55f, alpha)
                    : new Color(0.25f, 0.75f, 0.5f, alpha);
                GUI.color = fillColor;
                GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * _progress, barBg.height),
                                Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        // =====================================================================
        // 헬퍼
        // =====================================================================

        void BuildStyles()
        {
            _stylesReady = true;

            _sTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                richText  = true,
            };
            _sTitle.normal.textColor = Color.white;

            _sSub = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            _sSub.normal.textColor = Color.white;
        }
    }
}
