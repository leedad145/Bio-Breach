// =============================================================================
// WorldManager.cs - 전체 월드 청크 관리 (NGO 멀티플레이어)
// =============================================================================
// 흐름:
//   Server: Start() → seed 생성 → NetworkVariable에 저장 → 자신도 맵 생성
//   Client: OnSeedChanged 콜백 → 동일 seed로 맵 생성
//   지형 수정: Client → RequestModifyServerRpc → Server 적용 → BroadcastModifyClientRpc → 모든 클라이언트 적용
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using BioBreach.Engine.MarchingCubes;
using BioBreach.Engine.Voxel;
using BioBreach.Core.Voxel;

namespace BioBreach.Systems
{
    public class WorldManager : NetworkBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("플레이어")]
        [SerializeField] Transform player;

        [Header("청크 설정")]
        [SerializeField] VoxelMaterialMap voxelMaterialMap;
        [SerializeField] int viewDistance     = 5;
        [SerializeField] int size             = 20;
        [SerializeField] float voxelSize      = 3f;

        // =====================================================================
        // 네트워크 변수
        // =====================================================================

        // Seed: Server가 설정, 모든 Client가 수신 후 맵 생성
        private readonly NetworkVariable<Vector3> _netSeed = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // =====================================================================
        // 내부 변수
        // =====================================================================

        private float    _chunkWorldSize;
        private Vector3  _sharedSeed;
        private bool     _worldInitialized = false;

        private Dictionary<Vector3Int, MarchingChunk> _activeChunks  = new();
        private Stack<MarchingChunk>                  _chunkPool      = new();
        private Vector3Int _lastPlayerChunkPos = new(-999, -999, -999);

        private Queue<Vector3Int> _chunksToGenerate = new();
        private int _maxChunksPerFrame = 2;

        private readonly List<(Transform viewer, int dist)> _extraViewers = new();

        // =====================================================================
        // 공개 프로퍼티
        // =====================================================================

        public Dictionary<Vector3Int, MarchingChunk> ActiveChunks  => _activeChunks;
        public float ChunkWorldSize                                  => _chunkWorldSize;

        // =====================================================================
        // NGO 생명주기
        // =====================================================================

        public override void OnNetworkSpawn()
        {
            _chunkWorldSize = size * voxelSize;

            int side     = viewDistance * 2 + 1;
            int poolSize = side * side * side + 30;
            for (int i = 0; i < poolSize; i++)
                _chunkPool.Push(CreatePooledChunk());

            if (IsServer)
            {
                // Server: seed 생성 → NetworkVariable에 저장
                Vector3 seed = new Vector3(Random.value, Random.value, Random.value) * 10000f;
                _netSeed.Value = seed;
                InitializeWorld(seed);
            }
            else
            {
                // Client: OnValueChanged 구독 (이후 변경 대비) + 이미 값이 있으면 즉시 초기화
                // NGO 2.x 씬 오브젝트는 OnNetworkSpawn 시 NetworkVariable이 동기화 완료되지 않는 경우가 있어
                // Update()에 fallback 초기화 로직이 별도로 있음
                _netSeed.OnValueChanged += OnSeedChanged;
                if (_netSeed.Value.sqrMagnitude > 0.001f)
                    InitializeWorld(_netSeed.Value);
            }

            Debug.Log($"[WorldManager] OnNetworkSpawn — IsServer={IsServer}, PoolSize={poolSize}");
        }

        public override void OnNetworkDespawn()
        {
            _netSeed.OnValueChanged -= OnSeedChanged;
        }

        private void OnSeedChanged(Vector3 _, Vector3 next)
        {
            if (!_worldInitialized)
                InitializeWorld(next);
        }

        private void InitializeWorld(Vector3 seed)
        {
            _sharedSeed       = seed;
            _worldInitialized = true;
            Debug.Log($"[WorldManager] 월드 초기화 — Seed={seed}");
        }

        // =====================================================================
        // Update
        // =====================================================================

        void Update()
        {
            // 클라이언트 fallback: OnNetworkSpawn 시점에 seed를 못 받은 경우 재시도
            if (!_worldInitialized && !IsServer && _netSeed.Value.sqrMagnitude > 0.001f)
                InitializeWorld(_netSeed.Value);

            if (!_worldInitialized) return;

            // 뷰어 우선순위: Inspector 지정 player → 등록된 뷰어 → NGO LocalClient PlayerObject
            Transform mainViewer = player;
            if (mainViewer == null && _extraViewers.Count > 0)
                mainViewer = _extraViewers[0].viewer;
            if (mainViewer == null)
            {
                var localPlayerObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
                if (localPlayerObj != null)
                {
                    mainViewer = localPlayerObj.transform;
                    RegisterViewer(mainViewer, viewDistance); // 이후엔 extraViewers에서 찾도록 등록
                }
            }
            if (mainViewer == null) return;

            Vector3Int currentPlayerChunkPos = WorldToChunkPos(mainViewer.position);
            bool needsUpdate = currentPlayerChunkPos != _lastPlayerChunkPos;

            if (!needsUpdate)
            {
                for (int i = _extraViewers.Count - 1; i >= 0; i--)
                {
                    if (_extraViewers[i].viewer == null) { _extraViewers.RemoveAt(i); continue; }
                    Vector3Int vp = WorldToChunkPos(_extraViewers[i].viewer.position);
                    if (!_activeChunks.ContainsKey(vp)) { needsUpdate = true; break; }
                }
            }

            if (needsUpdate)
            {
                UpdateChunks(currentPlayerChunkPos);
                _lastPlayerChunkPos = currentPlayerChunkPos;
            }

            ProcessGenerationQueue();
        }

        // =====================================================================
        // 추가 뷰어
        // =====================================================================

        public void RegisterViewer(Transform viewer, int chunkDist = 1)
        {
            if (viewer == null) return;
            for (int i = 0; i < _extraViewers.Count; i++)
                if (_extraViewers[i].viewer == viewer) return;
            _extraViewers.Add((viewer, chunkDist));
        }

        public void UnregisterViewer(Transform viewer)
        {
            for (int i = _extraViewers.Count - 1; i >= 0; i--)
                if (_extraViewers[i].viewer == viewer)
                    _extraViewers.RemoveAt(i);
        }

        // =====================================================================
        // 청크 관리
        // =====================================================================

        Vector3Int WorldToChunkPos(Vector3 worldPos) => new(
            Mathf.FloorToInt(worldPos.x / _chunkWorldSize),
            Mathf.FloorToInt(worldPos.y / _chunkWorldSize),
            Mathf.FloorToInt(worldPos.z / _chunkWorldSize)
        );

        void UpdateChunks(Vector3Int playerPos)
        {
            var keysToRemove = new List<Vector3Int>();
            foreach (var coord in _activeChunks.Keys)
            {
                if (IsWithinAnyViewer(coord, playerPos)) continue;
                keysToRemove.Add(coord);
            }
            foreach (var key in keysToRemove)
            {
                ReturnToPool(_activeChunks[key]);
                _activeChunks.Remove(key);
            }

            _chunksToGenerate.Clear();
            var toAdd = new List<(Vector3Int coord, int dist)>();

            for (int x = playerPos.x - viewDistance; x <= playerPos.x + viewDistance; x++)
            for (int y = playerPos.y - viewDistance; y <= playerPos.y + viewDistance; y++)
            for (int z = playerPos.z - viewDistance; z <= playerPos.z + viewDistance; z++)
            {
                var coord = new Vector3Int(x, y, z);
                if (_activeChunks.ContainsKey(coord)) continue;
                int dist = Mathf.Abs(x - playerPos.x)
                         + Mathf.Abs(y - playerPos.y)
                         + Mathf.Abs(z - playerPos.z);
                toAdd.Add((coord, dist));
            }

            for (int i = _extraViewers.Count - 1; i >= 0; i--)
            {
                if (_extraViewers[i].viewer == null) { _extraViewers.RemoveAt(i); continue; }
                var (viewer, vd) = _extraViewers[i];
                Vector3Int vp = WorldToChunkPos(viewer.position);
                for (int x = vp.x - vd; x <= vp.x + vd; x++)
                for (int y = vp.y - vd; y <= vp.y + vd; y++)
                for (int z = vp.z - vd; z <= vp.z + vd; z++)
                {
                    var coord = new Vector3Int(x, y, z);
                    if (_activeChunks.ContainsKey(coord)) continue;
                    int dist = Mathf.Abs(x - vp.x) + Mathf.Abs(y - vp.y) + Mathf.Abs(z - vp.z);
                    toAdd.Add((coord, dist));
                }
            }

            toAdd.Sort((a, b) => a.dist.CompareTo(b.dist));
            foreach (var item in toAdd)
                _chunksToGenerate.Enqueue(item.coord);
        }

        bool IsWithinAnyViewer(Vector3Int coord, Vector3Int playerPos)
        {
            int dx = Mathf.Abs(coord.x - playerPos.x);
            int dz = Mathf.Abs(coord.z - playerPos.z);
            if (dx <= viewDistance + 1 && dz <= viewDistance + 1) return true;

            for (int i = 0; i < _extraViewers.Count; i++)
            {
                if (_extraViewers[i].viewer == null) continue;
                Vector3Int vp = WorldToChunkPos(_extraViewers[i].viewer.position);
                int vd = _extraViewers[i].dist;
                if (Mathf.Abs(coord.x - vp.x) <= vd + 1 &&
                    Mathf.Abs(coord.y - vp.y) <= vd + 1 &&
                    Mathf.Abs(coord.z - vp.z) <= vd + 1)
                    return true;
            }
            return false;
        }

        void ProcessGenerationQueue()
        {
            int generated = 0;
            while (_chunksToGenerate.Count > 0 && generated < _maxChunksPerFrame)
            {
                Vector3Int coord = _chunksToGenerate.Dequeue();
                if (_activeChunks.ContainsKey(coord)) continue;

                if (_chunkPool.Count == 0)
                {
                    Debug.LogWarning("[WorldManager] 풀 부족!");
                    _chunkPool.Push(CreatePooledChunk());
                }

                MarchingChunk chunk = _chunkPool.Pop();
                chunk.SetMaterialMap(voxelMaterialMap);
                chunk.gameObject.SetActive(true);
                chunk.gameObject.name = $"Chunk_{coord.x}_{coord.y}_{coord.z}";
                chunk.transform.position = new Vector3(coord.x, coord.y, coord.z) * _chunkWorldSize;
                chunk.CreateChunk(size, size, size, voxelSize, _sharedSeed);

                _activeChunks.Add(coord, chunk);
                generated++;
            }
        }

        MarchingChunk CreatePooledChunk()
        {
            var obj = new GameObject("Chunk_Pooled");
            obj.SetActive(false);
            obj.transform.SetParent(transform);
            return obj.AddComponent<MarchingChunk>();
        }

        void ReturnToPool(MarchingChunk chunk)
        {
            chunk.gameObject.SetActive(false);
            chunk.gameObject.name = "Chunk_Pooled";
            chunk.ClearDensityData();

            var mf = chunk.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh) mf.sharedMesh.Clear();

            var mc = chunk.GetComponent<MeshCollider>();
            if (mc) mc.sharedMesh = null;

            _chunkPool.Push(chunk);
        }

        // =====================================================================
        // 지형 수정 — 네트워크 동기화
        // =====================================================================

        /// <summary>
        /// Client에서 호출 → Server를 통해 다른 클라이언트에 동기화.
        /// 클라이언트는 렉 방지를 위해 로컬에 즉시 적용하고, 서버는 요청자를 제외한 나머지에게 브로드캐스트.
        /// </summary>
        public float ModifyTerrain(Vector3 worldPoint, float radius, float strength,
                                   VoxelType placeType = VoxelType.Air)
        {
            if (IsServer)
            {
                // Server 로컬 적용 후 모든 Client에게 브로드캐스트
                float result = ApplyModifyLocally(worldPoint, radius, strength, placeType);
                BroadcastModifyClientRpc(worldPoint, radius, strength, (byte)placeType);
                return result;
            }
            else
            {
                // Client: 로컬 즉시 적용(렉 없음) + 서버에 동기화 요청
                float result = ApplyModifyLocally(worldPoint, radius, strength, placeType);
                RequestModifyServerRpc(worldPoint, radius, strength, (byte)placeType);
                return result;
            }
        }

        /// <summary>Client → Server: 지형 수정 동기화 요청</summary>
        [ServerRpc(RequireOwnership = false)]
        private void RequestModifyServerRpc(Vector3 worldPoint, float radius, float strength,
                                            byte placeType, ServerRpcParams rpcParams = default)
        {
            ApplyModifyLocally(worldPoint, radius, strength, (VoxelType)placeType);

            // 요청한 클라이언트는 이미 로컬 적용했으므로 제외하고 브로드캐스트
            ulong senderId = rpcParams.Receive.SenderClientId;
            var   targets  = new List<ulong>();
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                if (id != senderId) targets.Add(id);

            if (targets.Count > 0)
            {
                var p = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = targets } };
                BroadcastModifyClientRpc(worldPoint, radius, strength, placeType, p);
            }
        }

        /// <summary>Server → 대상 Client들: 지형 수정 이벤트</summary>
        [ClientRpc]
        private void BroadcastModifyClientRpc(Vector3 worldPoint, float radius, float strength,
                                              byte placeType, ClientRpcParams clientRpcParams = default)
        {
            if (IsServer) return; // Host인 경우 이미 적용됨
            ApplyModifyLocally(worldPoint, radius, strength, (VoxelType)placeType);
        }

        private float ApplyModifyLocally(Vector3 worldPoint, float radius, float strength,
                                         VoxelType placeType)
        {
            float margin   = radius + voxelSize;
            float totalDug = 0f;
            foreach (var kvp in _activeChunks)
            {
                if (kvp.Value.ContainsPoint(worldPoint, margin))
                    totalDug += kvp.Value.ModifyDensity(worldPoint, radius, strength, placeType);
            }
            return totalDug;
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        public Vector3Int WorldToChunkCoord(Vector3 worldPos) => WorldToChunkPos(worldPos);

        // =====================================================================
        // 기즈모
        // =====================================================================

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || player == null) return;
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Vector3Int p = WorldToChunkPos(player.position);
            for (int x = p.x - viewDistance; x <= p.x + viewDistance; x++)
            for (int y = p.y - viewDistance; y <= p.y + viewDistance; y++)
            for (int z = p.z - viewDistance; z <= p.z + viewDistance; z++)
            {
                Gizmos.DrawWireCube(
                    new Vector3(x, y, z) * _chunkWorldSize + Vector3.one * (_chunkWorldSize * 0.5f),
                    Vector3.one * _chunkWorldSize
                );
            }
        }
    }
}
