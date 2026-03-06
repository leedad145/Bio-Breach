using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Threading.Tasks;
using System;
using UnityEngine.UI;

namespace BioBreach.UI.Lobbies
{
    public class AuthUI : MonoBehaviour
    {
        [SerializeField] Button _confirm;
        [SerializeField] GameObject _successAlert;
        [SerializeField] GameObject _failureAlert;


        private void OnEnable()
        {
            _confirm.onClick.AddListener(Authenticate);
        }


        public async void Authenticate()
        {
            bool result = await AuthenticateAsync();

            if (result)
            {
                _successAlert.SetActive(true);
            }
            else
            {
                _failureAlert.SetActive(true);
            }
        }

        private async Task<bool> AuthenticateAsync()
        {
            if (!AuthenticationService.Instance.IsAuthorized)
            {
                try
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                catch (AuthenticationException ex)
                {
                    Debug.LogError($"인증 실패: {ex.ErrorCode} {ex.Message}");
                    return false;
                }
                catch (RequestFailedException ex)
                {
                    Debug.LogError($"인증 실패: {ex.ErrorCode} {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    return false;
                }
            }

            Debug.Log($"인증 성공 PlayerId : {AuthenticationService.Instance.PlayerId}");
            return true;
        }
    }
}