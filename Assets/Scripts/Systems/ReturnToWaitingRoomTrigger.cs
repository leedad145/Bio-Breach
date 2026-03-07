// =============================================================================
// ReturnToWaitingRoomTrigger.cs - 대기실 복귀 상호작용 오브젝트
//
// Matriarch 근처에 배치. 모든 플레이어가 근처에서 E키를 누르면
// 호스트가 WaitingRoom 씬으로 전환한다.
// 반경을 벗어나면 투표가 자동 취소된다.
// =============================================================================
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using BioBreach.Systems;

namespace BioBreach.Systems
{
    public class ReturnToWaitingRoomTrigger : NetworkBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("상호작용")]
        [Tooltip("상호작용 가능 거리 (m)")]
        [SerializeField] float   _interactRadius = 5f;
        [Tooltip("상호작용 키")]
        [SerializeField] KeyCode _interactKey    = KeyCode.E;

        // =====================================================================
        // 네트워크 상태
        // =====================================================================

        // 현재 투표한 플레이어 수 / 전체 플레이어 수 (모든 클라이언트에서 읽기)
        readonly NetworkVariable<int> _readyCount = new NetworkVariable<int>(0);
        readonly NetworkVariable<int> _totalCount = new NetworkVariable<int>(0);

        // Server 전용: 투표한 클라이언트 ID 집합
        readonly HashSet<ulong> _readyClients = new HashSet<ulong>();

        // =====================================================================
        // 클라이언트 로컬 상태
        // =====================================================================

        GameManager _gameManager;
        Transform   _localPlayer;
        bool        _isReady;       // 이 클라이언트가 투표 중인지
        bool        _wasNearby;     // 지난 프레임 근접 여부 (이탈 감지용)
        GUIStyle    _hudStyle;

        // =====================================================================
        // 초기화
        // =====================================================================

        void Start()
        {
            _gameManager = FindAnyObjectByType<GameManager>();
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            if (!IsSpawned) return;

            // 로컬 플레이어 Transform 캐시
            if (_localPlayer == null)
            {
                var playerObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
                if (playerObj != null) _localPlayer = playerObj.transform;
            }
            if (_localPlayer == null) return;

            bool nearby = Vector3.Distance(transform.position, _localPlayer.position) <= _interactRadius;

            // 반경 이탈 → 투표 취소
            if (_wasNearby && !nearby && _isReady)
            {
                _isReady = false;
                CancelReadyServerRpc();
            }
            _wasNearby = nearby;

            if (!nearby) return;

            // E키 → 투표 토글
            if (Input.GetKeyDown(_interactKey))
            {
                if (!_isReady)
                {
                    _isReady = true;
                    RequestReadyServerRpc();
                }
                else
                {
                    _isReady = false;
                    CancelReadyServerRpc();
                }
            }
        }

        // =====================================================================
        // ServerRpc
        // =====================================================================

        [ServerRpc(RequireOwnership = false)]
        void RequestReadyServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong id = rpcParams.Receive.SenderClientId;
            if (!_readyClients.Add(id)) return;

            _totalCount.Value = NetworkManager.ConnectedClientsIds.Count;
            _readyCount.Value = _readyClients.Count;

            // 전원 투표 완료
            if (_readyCount.Value >= _totalCount.Value && _totalCount.Value > 0)
                _gameManager?.ReturnToWaitingRoom();
        }

        [ServerRpc(RequireOwnership = false)]
        void CancelReadyServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong id = rpcParams.Receive.SenderClientId;
            if (!_readyClients.Remove(id)) return;

            _totalCount.Value = NetworkManager.ConnectedClientsIds.Count;
            _readyCount.Value = _readyClients.Count;
        }

        // =====================================================================
        // UI
        // =====================================================================

        void OnGUI()
        {
            if (!IsSpawned) return;
            if (_localPlayer == null) return;

            bool nearby = Vector3.Distance(transform.position, _localPlayer.position) <= _interactRadius;
            if (!nearby && _readyCount.Value == 0) return;

            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize  = 14,
                    alignment = TextAnchor.MiddleCenter,
                };
                _hudStyle.normal.textColor = Color.white;
            }

            float w  = 260f;
            float h  = 54f;
            float cx = Screen.width  * 0.5f;
            float cy = Screen.height * 0.72f;

            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(cx - w * 0.5f, cy, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string readyLine = $"대기실 복귀 준비: {_readyCount.Value} / {_totalCount.Value}명";
            string actionLine;
            if (nearby)
                actionLine = _isReady ? "[E] 취소" : "[E] 준비";
            else
                actionLine = "가까이 이동하세요";

            GUI.Label(new Rect(cx - w * 0.5f, cy + 4f,  w, 22f), readyLine,  _hudStyle);
            GUI.Label(new Rect(cx - w * 0.5f, cy + 28f, w, 22f), actionLine, _hudStyle);
        }

        // =====================================================================
        // 기즈모
        // =====================================================================

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
    }
}
