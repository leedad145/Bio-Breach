using System;
using System.Collections;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace BioBreach.Systems
{
    public enum GameState
    {
        None,
        Authenticating,
        Authenticated,
        Connecting,
        WaitingRoom,            // 세션 참가 직후 → WaitingRoom 씬 로드 요청
        InWaitingRoom,          // WaitingRoom 씬 안에서 플레이어가 대기 중
        Connected,
        InGameInitializing,
        InGameInitialized,
        InGamePlaying
    }


    public class GameManager : MonoBehaviour
    {
        public GameState State { get; private set; }

        /// <summary>WaitingRoom 씬에서 호스트가 GameStartTrigger를 사용했을 때 호출</summary>
        public void StartGame()
        {
            if (State == GameState.InWaitingRoom)
                State = GameState.Connected;
        }

        /// <summary>게임 중 모든 플레이어가 대기실로 돌아갈 때 호출</summary>
        public void ReturnToWaitingRoom()
        {
            if (State == GameState.InGamePlaying
             || State == GameState.InGameInitialized
             || State == GameState.InGameInitializing)
                State = GameState.WaitingRoom;
        }

        private void Awake()
        {
            // Bootstrap 씬에서 살아남아 Title/Game 씬 전환 후에도 유지
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                try
                {
                    await UnityServices.InitializeAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"초기화 실패. {ex.Message}");

                }
            }
            
            StartCoroutine(Workflow());
        }

        IEnumerator Workflow()
        {
            while (true)
            {
                switch (State)
                {
                    case GameState.None:
                        {
                            AsyncOperation op = SceneManager.LoadSceneAsync("Title");

                            while (!op.isDone)
                            {
                                Debug.Log($"Title 씬 로딩중 .. 진행률. {op.progress}");
                                yield return null;
                            }

                            State = GameState.Authenticating;
                        }
                        break;
                    case GameState.Authenticating:
                        {
                            if (AuthenticationService.Instance.IsSignedIn)
                            {
                                State = GameState.Authenticated;
                            }
                        }
                        break;
                    case GameState.Authenticated:
                        {

                            State = GameState.Connecting;
                        }
                        break;
                    case GameState.Connecting:
                        {
                            if (SessionBlackboard.CurrentSession != null)
                                State = GameState.WaitingRoom;
                        }
                        break;
                    case GameState.WaitingRoom:
                        {
                            // Host가 WaitingRoom 씬 로드 → NGO SceneManager가 모든 클라이언트에 동기화
                            if (NetworkManager.Singleton.IsHost)
                                NetworkManager.Singleton.SceneManager.LoadScene("WaitingRoom", LoadSceneMode.Single);

                            State = GameState.InWaitingRoom;
                        }
                        break;
                    case GameState.InWaitingRoom:
                        {
                            // 세션 끊김 → NGO 종료 후 Title 씬으로 복귀
                            if (SessionBlackboard.CurrentSession == null
                                || !SessionBlackboard.CurrentSession.IsMember)
                            {
                                if (NetworkManager.Singleton != null)
                                    NetworkManager.Singleton.Shutdown();

                                yield return null; // Shutdown 대기

                                AsyncOperation op = SceneManager.LoadSceneAsync("Title");
                                while (!op.isDone) { yield return null; }

                                SessionBlackboard.CurrentSession = null;
                                State = GameState.Connecting;
                            }
                            // StartGame() 호출 시 → Connected 로 전환 (GameStartTrigger가 호출)
                        }
                        break;
                    case GameState.Connected:
                        {
                            // 호스트만 씬 로드 — NGO SceneManager가 모든 클라이언트에 자동 동기화
                            if (NetworkManager.Singleton.IsHost)
                                NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);

                            State = GameState.InGameInitializing;
                        }
                        break;
                    case GameState.InGameInitializing:
                        {
                            if (SceneManager.GetSceneByName("Game").isLoaded)
                            {
                                State = GameState.InGameInitialized;
                            }
                        }
                        break;
                    case GameState.InGameInitialized:
                        {
                            State = GameState.InGamePlaying;
                        }
                        break;
                    case GameState.InGamePlaying:
                        {
                            if (SessionBlackboard.CurrentSession == null || !SessionBlackboard.CurrentSession.IsMember)
                            {
                                if (NetworkManager.Singleton != null)
                                    NetworkManager.Singleton.Shutdown();

                                // Shutdown 후 NGO가 씬을 정리할 때까지 한 프레임 대기
                                yield return null;

                                if (SceneManager.GetSceneByName("Game").isLoaded)
                                {
                                    AsyncOperation op = SceneManager.UnloadSceneAsync("Game", UnloadSceneOptions.None);
                                    while (!op.isDone)
                                    {
                                        Debug.Log($"Game 씬 언로딩중 .. 진행률. {op.progress}");
                                        yield return null;
                                    }
                                }

                                SessionBlackboard.CurrentSession = null;
                                State = GameState.Authenticated;
                            }
                        }
                        break;
                    default:
                        break;
                }

                yield return null;
            }
        }
    }
}