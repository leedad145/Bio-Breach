// =============================================================================
// WaveHUD.cs - 인게임 웨이브 카운트다운 + 성체 HP 바 HUD
// =============================================================================
using UnityEngine;
using BioBreach.Controller.Enemy;
using BioBreach.Controller.Matriarch;

namespace BioBreach.UI
{
    public class WaveHUD : MonoBehaviour
    {
        // =====================================================================
        // 레이아웃 상수
        // =====================================================================

        const int HudW      = 320;
        const int WaveH     = 52;
        const int MatH      = 52;
        const int Padding   = 10;
        const int BarH      = 14;

        // =====================================================================
        // 내부 참조 (씬 오브젝트 탐색)
        // =====================================================================

        EnemySpawner      _spawner;
        MatriarchController _matriarch;

        // GUIStyle 캐시
        GUIStyle _sLabel, _sSmall;
        bool     _stylesReady;

        // 웨이브 클리어 알림
        float   _clearNotifyUntil;
        int     _lastNotifiedWave = -1;

        // =====================================================================
        // 초기화
        // =====================================================================

        void OnEnable()
        {
            EnemySpawner.OnWaveCleared += HandleWaveCleared;
        }

        void OnDisable()
        {
            EnemySpawner.OnWaveCleared -= HandleWaveCleared;
        }

        void HandleWaveCleared(int waveIndex)
        {
            if (waveIndex != _lastNotifiedWave)
            {
                _lastNotifiedWave  = waveIndex;
                _clearNotifyUntil  = Time.time + 3f;
            }
        }

        // =====================================================================
        // 업데이트 — 참조 캐시
        // =====================================================================

        void Update()
        {
            if (_spawner   == null) _spawner   = FindAnyObjectByType<EnemySpawner>();
            if (_matriarch == null) _matriarch = FindAnyObjectByType<MatriarchController>();
        }

        // =====================================================================
        // OnGUI
        // =====================================================================

        void OnGUI()
        {
            if (_spawner == null && _matriarch == null) return;
            EnsureStyles();

            float px = (Screen.width - HudW) * 0.5f;

            // ── 웨이브 패널 (상단 중앙) ────────────────────────────────────────
            if (_spawner != null)
            {
                Rect wavePanel = new Rect(px, Padding, HudW, WaveH);
                DrawPanel(wavePanel, new Color(0.04f, 0.04f, 0.08f, 0.88f));

                int   wave      = _spawner.WaveNumber;
                float remaining = Mathf.Max(0f, _spawner.NextWaveTime - Time.time);

                GUI.color = new Color(0.9f, 0.85f, 0.4f);
                GUI.Label(new Rect(wavePanel.x + Padding, wavePanel.y + 6, HudW - Padding * 2, 18),
                          $"<b>웨이브 {wave + 1}</b>", _sLabel);

                if (Time.time < _clearNotifyUntil)
                {
                    // 웨이브 클리어 알림
                    float alpha = Mathf.Clamp01((_clearNotifyUntil - Time.time) / 1f);
                    GUI.color = new Color(0.3f, 1f, 0.4f, alpha);
                    GUI.Label(new Rect(wavePanel.x + Padding, wavePanel.y + 24, HudW - Padding * 2, 18),
                              "웨이브 클리어!", _sLabel);
                }
                else if (remaining > 0.5f)
                {
                    GUI.color = new Color(0.7f, 0.8f, 1.0f);
                    GUI.Label(new Rect(wavePanel.x + Padding, wavePanel.y + 26, HudW - Padding * 2, 16),
                              $"다음 웨이브까지  {remaining:F0}초", _sSmall);

                    // 카운트다운 바
                    float interval = _spawner.SpawnInterval + _spawner.CurrentSpawnDelayBonus;
                    float ratio    = interval > 0f ? remaining / interval : 0f;
                    DrawBar(new Rect(wavePanel.x + Padding, wavePanel.y + WaveH - BarH - 6,
                                     HudW - Padding * 2, BarH),
                            ratio, new Color(0.3f, 0.5f, 1f, 0.8f), new Color(0.1f, 0.1f, 0.2f, 0.6f));
                }
                else
                {
                    GUI.color = new Color(1f, 0.4f, 0.3f);
                    GUI.Label(new Rect(wavePanel.x + Padding, wavePanel.y + 26, HudW - Padding * 2, 16),
                              "적 소환 중!", _sSmall);
                }
            }

            // ── 성체 HP 패널 (상단 중앙, 웨이브 패널 아래) ─────────────────────
            if (_matriarch != null)
            {
                float topY     = Padding + (_spawner != null ? WaveH + 4 : 0);
                Rect  matPanel = new Rect(px, topY, HudW, MatH);
                DrawPanel(matPanel, new Color(0.04f, 0.08f, 0.05f, 0.88f));

                float hp    = _matriarch.CurrentHp;
                float maxHp = _matriarch.MaxHp;
                float ratio = maxHp > 0f ? hp / maxHp : 0f;

                // HP 텍스트
                Color hpColor = ratio > 0.5f ? new Color(0.4f, 1f, 0.5f) :
                                ratio > 0.2f ? new Color(1f, 0.8f, 0.2f) :
                                               new Color(1f, 0.3f, 0.2f);
                GUI.color = hpColor;
                GUI.Label(new Rect(matPanel.x + Padding, matPanel.y + 6, HudW - Padding * 2, 18),
                          $"<b>성체 HP  {hp:F0} / {maxHp:F0}</b>", _sLabel);

                // HP 바
                DrawBar(new Rect(matPanel.x + Padding, matPanel.y + MatH - BarH - 6,
                                  HudW - Padding * 2, BarH),
                        ratio, hpColor, new Color(0.08f, 0.14f, 0.09f, 0.7f));
            }

            GUI.color = Color.white;
        }

        // =====================================================================
        // 헬퍼
        // =====================================================================

        static void DrawPanel(Rect r, Color bg)
        {
            GUI.color = bg;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.35f, 0.35f, 0.45f, 0.7f);
            // border
            GUI.DrawTexture(new Rect(r.x,        r.y,         r.width, 1),        Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,        r.yMax - 1,  r.width, 1),        Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,        r.y,         1,       r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1, r.y,         1,       r.height), Texture2D.whiteTexture);
        }

        static void DrawBar(Rect r, float ratio, Color fillColor, Color bgColor)
        {
            GUI.color = bgColor;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            if (ratio > 0f)
            {
                GUI.color = fillColor;
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * ratio, r.height), Texture2D.whiteTexture);
            }
        }

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _sLabel = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, richText = true };
            _sLabel.normal.textColor = Color.white;

            _sSmall = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            _sSmall.normal.textColor = Color.white;
        }
    }
}
