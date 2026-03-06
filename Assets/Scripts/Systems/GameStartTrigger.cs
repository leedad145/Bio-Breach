// =============================================================================
// GameStartTrigger.cs - WaitingRoom 씬의 게임 시작 상호작용 오브젝트
//
// 호스트가 이 오브젝트 근처에서 E키를 누르면 게임이 시작된다.
// 클라이언트에게는 "호스트 대기 중" 안내만 표시된다.
// =============================================================================
using Unity.Netcode;
using UnityEngine;
using BioBreach.Systems;

namespace BioBreach.Systems
{
    public class GameStartTrigger : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("상호작용")]
        [Tooltip("상호작용 가능 거리 (m)")]
        [SerializeField] float    _interactRadius = 3f;
        [Tooltip("상호작용 키")]
        [SerializeField] KeyCode  _interactKey    = KeyCode.E;

        [Header("프롬프트 오브젝트")]
        [Tooltip("호스트 전용 - 근처 접근 시 표시 ('E: 게임 시작' 등)")]
        [SerializeField] GameObject _hostPrompt;
        [Tooltip("클라이언트 전용 - 항상 표시 ('호스트가 시작하기를 기다리는 중...' 등)")]
        [SerializeField] GameObject _clientPrompt;

        // =====================================================================
        // 내부 변수
        // =====================================================================

        GameManager _gameManager;
        Transform   _localPlayer;
        bool        _isHost;
        bool        _promptVisible;

        // =====================================================================
        // 초기화
        // =====================================================================

        void Start()
        {
            _gameManager = FindAnyObjectByType<GameManager>();
            _isHost      = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

            // 초기 프롬프트 상태
            if (_hostPrompt)   _hostPrompt.SetActive(false);
            if (_clientPrompt) _clientPrompt.SetActive(!_isHost);
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            if (!_isHost) return;

            // 로컬 플레이어 Transform 캐시 (스폰 직후 1회)
            if (_localPlayer == null)
            {
                var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (playerObj != null)
                    _localPlayer = playerObj.transform;
            }

            if (_localPlayer == null) return;

            // 거리 체크
            bool nearby = Vector3.Distance(transform.position, _localPlayer.position) <= _interactRadius;

            if (nearby != _promptVisible)
            {
                _promptVisible = nearby;
                if (_hostPrompt) _hostPrompt.SetActive(_promptVisible);
            }

            // 상호작용
            if (_promptVisible && Input.GetKeyDown(_interactKey))
                _gameManager?.StartGame();
        }

        // =====================================================================
        // 기즈모
        // =====================================================================

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
    }
}
