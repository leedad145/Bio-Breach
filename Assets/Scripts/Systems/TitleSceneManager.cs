using UnityEngine;

namespace BioBreach.Systems
{
    /// <summary>
    /// Title 씬 UI 제어.
    /// GameManager의 State를 폴링하여 AuthPanel / LobbiesPanel을 전환한다.
    /// </summary>
    public class TitleSceneManager : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField] GameObject _authPanel;
        [SerializeField] GameObject _lobbiesPanel;

        GameManager _gameManager;
        GameState   _lastState = (GameState)(-1); // 최초 강제 갱신용

        void Start()
        {
            _gameManager = FindAnyObjectByType<GameManager>();

            if (_gameManager == null)
                Debug.LogError("[TitleSceneManager] GameManager를 찾을 수 없습니다. Bootstrap 씬이 먼저 로드되어야 합니다.");

            // 초기 패널 상태 설정
            ApplyState(_gameManager != null ? _gameManager.State : GameState.Authenticating);
        }

        void Update()
        {
            if (_gameManager == null) return;

            GameState current = _gameManager.State;
            if (current == _lastState) return;

            _lastState = current;
            ApplyState(current);
        }

        void ApplyState(GameState state)
        {
            switch (state)
            {
                case GameState.Authenticating:
                    Show(_authPanel);
                    Hide(_lobbiesPanel);
                    break;

                // Authenticated → Connecting 은 즉시 전환됨
                // Connecting = 세션 대기 중 → LobbiesUI 표시
                case GameState.Connecting:
                    Hide(_authPanel);
                    Show(_lobbiesPanel);
                    break;

                // WaitingRoom 이후는 WaitingRoom 씬으로 전환 중 → 전부 숨김
                case GameState.WaitingRoom:
                case GameState.InWaitingRoom:
                case GameState.Connected:
                case GameState.InGameInitializing:
                case GameState.InGameInitialized:
                case GameState.InGamePlaying:
                    Hide(_authPanel);
                    Hide(_lobbiesPanel);
                    break;
            }
        }

        static void Show(GameObject panel) { if (panel) panel.SetActive(true);  }
        static void Hide(GameObject panel) { if (panel) panel.SetActive(false); }
    }
}
