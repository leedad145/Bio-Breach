using System;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BioBreach.Systems;

namespace BioBreach.UI.Lobbies
{
    public class LobbiesSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] TMP_Text _roomTitle;
        [SerializeField] TMP_Text _roomPlayerCount;
        [SerializeField] Button _join;
        [SerializeField] GameObject _joinAbleVisual;
        [SerializeField] GameObject _fullVisual;
        CanvasGroup _canvasGroup;
        ISessionInfo _sessionInfo;
        void OnEnable()
        {
            _join.onClick.AddListener(JoinLobbyAsync);
        }
        void Start()
        {
            _canvasGroup = GetComponentInParent<CanvasGroup>();
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if(_sessionInfo.AvailableSlots > 0)
            {
                _joinAbleVisual.SetActive(true);
                _join.interactable = true;
            }
            else
            {
                _fullVisual.SetActive(true);
                _join.interactable = false;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _joinAbleVisual.SetActive(false);
            _fullVisual.SetActive(false);
        }

        public void Refresh(ISessionInfo sesisonInfo)
        {
            _roomTitle.text = sesisonInfo.Name;
            _roomPlayerCount.text = $"{sesisonInfo.MaxPlayers - sesisonInfo.AvailableSlots}/{sesisonInfo.MaxPlayers}";
            _sessionInfo = sesisonInfo;
        }
        async void JoinLobbyAsync()
        {
            _canvasGroup.interactable = false;
            try
            {
                SessionBlackboard.CurrentSession = await MultiplayerService.Instance.JoinSessionByIdAsync(_sessionInfo.Id);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
            finally
            {
                _canvasGroup.interactable = true;
            }
        }
    }
}

