using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;
using BioBreach.Systems;

namespace BioBreach.UI.Lobbies
{
    public class LobbiesUI : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField] GameObject _connectionPanel;
        [SerializeField] Button _showCreateRoomPanel;

        [Header("CreateRoom")]
        [SerializeField] GameObject _createRoomPanel;
        [SerializeField] TMP_InputField _createRoomOptionTitle;
        [SerializeField] Toggle _createRoomOptionPrivacy;
        [SerializeField] Button _createRoomOptionMaxPlayerIncrement;
        [SerializeField] Button _createRoomOptionMaxPlayerDecrement;
        [SerializeField] TMP_Text _createRoomOptionMaxPlayerCount;
        int _createRoomOptionMaxPlayerCountValue;
        [SerializeField] Button _confirmCreateRoom;
        [SerializeField] Button _cancelCreateRoom;

        [Header("Waiting")]
        [SerializeField] GameObject _waitingPanel;

        [Header("JoinResponse")]
        [SerializeField] GameObject _joinSuccessPanel;
        [SerializeField] GameObject _joinFailurePanel;
        [Header("Lobbies")]
        [SerializeField] RectTransform _lobbiesContent;
        [SerializeField] LobbiesSlotUI _slotUIPrefab;
        List<LobbiesSlotUI> _slots = new List<LobbiesSlotUI>();
        [SerializeField] Button _refreshLobbies;
        [SerializeField] Button _quickJoin;
        [SerializeField] TMP_InputField _roomCodeInput;
        [SerializeField] Button _roomCodeJoin;
        [Header("JoinFailure")]
        [SerializeField] Button _done;

        GameObject _activePanel;

        const int MAX_PLAYERS_MIN = 2;
        const int MAX_PLAYERS_MAX = 12;

        #region Custom properties
        const string LEVEL_IDENTIFIER = "Level";

        #endregion
        private void OnEnable()
        {
            ShowConnectionPanel();
            _showCreateRoomPanel.onClick.AddListener(ShowCreateRoomPanel);
            _createRoomOptionMaxPlayerIncrement.onClick.AddListener(IncreaseCreateRoomOptionMaxPlayerCount);
            _createRoomOptionMaxPlayerDecrement.onClick.AddListener(DecreaseCreateRoomOptionMaxPlayerCount);
            _createRoomOptionMaxPlayerCountValue = MAX_PLAYERS_MIN;
            _createRoomOptionMaxPlayerCount.text = MAX_PLAYERS_MIN.ToString();
            _confirmCreateRoom.onClick.AddListener(OnConfirmCreateRoom);
            _cancelCreateRoom.onClick.AddListener(ShowConnectionPanel);
            _refreshLobbies.onClick.AddListener(RefreshLobbies);
            _quickJoin.onClick.AddListener(OnQuickJoinAsync);
            _roomCodeJoin.onClick.AddListener(OnCodeJoinAsync);
            _done.onClick.AddListener(ShowCreateRoomPanel);
            RefreshLobbies();
        }

        void ShowConnectionPanel()
        {
            _activePanel?.SetActive(false);
            _connectionPanel.SetActive(true);
            _activePanel = _connectionPanel;
        }

        void ShowCreateRoomPanel()
        {
            _activePanel?.SetActive(false);
            _createRoomPanel.SetActive(true);
            _activePanel = _createRoomPanel;
        }

        void ShowWaitingPanel()
        {
            _activePanel?.SetActive(false);
            _waitingPanel.SetActive(true);
            _activePanel = _waitingPanel;
        }

        void ShowJoinSuccessPanel()
        {
            _activePanel?.SetActive(false);
            _joinSuccessPanel.SetActive(true);
            _activePanel = _joinSuccessPanel;
        }

        void ShowJoinFailurePanel()
        {
            _activePanel?.SetActive(false);
            _joinFailurePanel.SetActive(true);
            _activePanel = _joinFailurePanel;
        }

        void IncreaseCreateRoomOptionMaxPlayerCount()
        {
            _createRoomOptionMaxPlayerCountValue = Mathf.Min(_createRoomOptionMaxPlayerCountValue + 1, MAX_PLAYERS_MAX);
            _createRoomOptionMaxPlayerCount.text = _createRoomOptionMaxPlayerCountValue.ToString();
        }

        void DecreaseCreateRoomOptionMaxPlayerCount()
        {
            _createRoomOptionMaxPlayerCountValue = Mathf.Max(_createRoomOptionMaxPlayerCountValue - 1, MAX_PLAYERS_MIN);
            _createRoomOptionMaxPlayerCount.text = _createRoomOptionMaxPlayerCountValue.ToString();
        }
        
        async void OnQuickJoinAsync()
        {
            try
            {
                QuerySessionsOptions options = new()
                {
                    Count = 1,
                };

                var result = await MultiplayerService.Instance.QuerySessionsAsync(options);

                if (result.Sessions == null || result.Sessions.Count == 0)
                {
                    Debug.Log("방 없음.");
                    return;
                }

                SessionBlackboard.CurrentSession = await MultiplayerService.Instance.JoinSessionByIdAsync(result.Sessions[0].Id);
            }
            catch (Exception ex)
            {
                Debug.LogError($"QuickJoin failed: {ex}");
            }
        }
        async void OnCodeJoinAsync()
        {
            try
            {
                SessionBlackboard.CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(_roomCodeInput.text.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError($"CodeJoin failed: {ex}");
            }
        }
        async void OnConfirmCreateRoom()
        {
            ShowWaitingPanel();

            var response = await CreateSessionAsync(roomName: _createRoomOptionTitle.text,
                                                    isPrivate: _createRoomOptionPrivacy.isOn,
                                                    maxPlayers: _createRoomOptionMaxPlayerCountValue);

            if (response.success)
            {
                ShowJoinSuccessPanel();
            }
            else
            {
                ShowJoinFailurePanel();
            }
        }

        async Task<(bool success, string message)> CreateSessionAsync(string roomName, bool isPrivate, int maxPlayers)
        {
            SessionOptions options = new SessionOptions
            {
                Name = roomName,
                IsPrivate = isPrivate,
                MaxPlayers = maxPlayers,
            };
            options.WithRelayNetwork(); // Host-Client 구조 (IsServer 기반 코드와 호환)
            
            Guid sessionId = Guid.NewGuid();
            try
            {
                SessionBlackboard.CurrentSession = await MultiplayerService.Instance.CreateOrJoinSessionAsync(sessionId.ToString(), options);
                Debug.Log($"SessionCode: {SessionBlackboard.CurrentSession.Code}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }

            return (true, string.Empty);
        }
        async void RefreshLobbies()
        {
            // TODO: Object Pooling
            for(int i = 0; i < _slots.Count; i++)
                Destroy(_slots[i].gameObject);

            _slots.Clear();

            QuerySessionsOptions options = new()
            {
                Count = 20,
            };

            try
            {
                var result = await MultiplayerService.Instance.QuerySessionsAsync(options);
                foreach(var session in result.Sessions)
                {
                    var slot = Instantiate(_slotUIPrefab, _lobbiesContent);
                    slot.Refresh(session);
                    _slots.Add(slot);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"RefreshLobbies Failed: {ex}");
                // 실패 UI 팝업
            }

        }
    }
}