// =============================================================================
// PlayerSpawnManager.cs - 씬이 준비된 후 플레이어를 스폰 포인트에 생성
//
// WaitingRoom 씬과 Game 씬 양쪽에 배치.
// NetworkManager의 Player Prefab 필드는 비워두고 이 스크립트가 대신 관리한다.
//
// 흐름:
//   OnNetworkSpawn (Server) → 이미 연결된 클라이언트 스폰
//   OnClientConnectedCallback → 이후에 연결되는 클라이언트 스폰
//   destroyWithScene: true → 씬 전환 시 기존 플레이어 자동 제거 → 새 씬에서 재스폰
// =============================================================================
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BioBreach.Systems
{
    public class PlayerSpawnManager : NetworkBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("플레이어 프리팹")]
        [Tooltip("NetworkObject 컴포넌트가 있어야 함. NetworkManager의 Player Prefab은 비워둘 것.")]
        [SerializeField] GameObject _playerPrefab;

        [Header("스폰 포인트 목록 (순서대로 배정)")]
        [SerializeField] Transform[] _spawnPoints;

        // =====================================================================
        // 내부 변수
        // =====================================================================

        int _nextSpawnIndex;

        // 스폰된 플레이어 추적 — OnNetworkDespawn에서 명시적 Despawn에 사용
        readonly List<NetworkObject> _spawnedPlayers = new();

        // =====================================================================
        // NGO 생명주기
        // =====================================================================

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            // 씬 로드 시점에 이미 연결된 클라이언트 (호스트 포함) 스폰
            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                if (client.PlayerObject == null)
                    SpawnPlayer(client.ClientId);
            }

            // 이후 접속하는 클라이언트 처리
            NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;

            // 씬 전환 시 NGO 내부 루프가 이미 파괴된 NetworkObject에 접근하는 것을 막기 위해
            // Unity가 씬 오브젝트를 파괴하기 전에 직접 Despawn한다.
            foreach (var netObj in _spawnedPlayers)
            {
                if (netObj != null && netObj.IsSpawned)
                    netObj.Despawn(destroy: true);
            }
            _spawnedPlayers.Clear();
        }

        // =====================================================================
        // 스폰 로직
        // =====================================================================

        void SpawnPlayer(ulong clientId)
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawnManager] playerPrefab이 비어 있습니다.");
                return;
            }
            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                Debug.LogError("[PlayerSpawnManager] SpawnPoint가 하나도 없습니다.");
                return;
            }

            Transform sp  = _spawnPoints[_nextSpawnIndex % _spawnPoints.Length];
            _nextSpawnIndex++;

            var go     = Instantiate(_playerPrefab, sp.position, sp.rotation);
            var netObj = go.GetComponent<NetworkObject>();

            if (netObj == null)
            {
                Debug.LogError("[PlayerSpawnManager] Player prefab에 NetworkObject 컴포넌트가 없습니다.");
                Destroy(go);
                return;
            }

            // destroyWithScene: false → OnNetworkDespawn에서 직접 제어
            netObj.SpawnAsPlayerObject(clientId, destroyWithScene: false);
            _spawnedPlayers.Add(netObj);
        }

        // =====================================================================
        // 기즈모
        // =====================================================================

        void OnDrawGizmosSelected()
        {
            if (_spawnPoints == null) return;
            Gizmos.color = Color.cyan;
            foreach (var sp in _spawnPoints)
            {
                if (sp == null) continue;
                Gizmos.DrawWireSphere(sp.position, 0.5f);
                Gizmos.DrawRay(sp.position, sp.forward * 1.5f);
            }
        }
    }
}
