// =============================================================================
// WorldManager.cs - 전체 월드 청크 관리 (NGO 멀티플레이어)
// =============================================================================
// 흐름:
//   Server: OnNetworkSpawn → seed 생성 → NetworkVariable에 저장 → 맵 전체 생성
//   Client: OnSeedChanged 콜백 → 동일 seed로 맵 전체 생성
//   지형 수정: Client → RequestModifyServerRpc → Server 적용 → BroadcastModifyClientRpc → 모든 클라이언트 적용
// =============================================================================

using System;
using System.Collections;
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

        [Header("청크 설정")]
        [SerializeField] VoxelMaterialMap voxelMaterialMap;
        [SerializeField] int   size      = 20;
        [SerializeField] float voxelSize = 0.5f;

        [Header("맵 크기")]
        [Tooltip("맵 반지름 (월드 단위). 이 범위 내의 청크가 전부 생성되고, 외부는 Wall 복셀로 막힘.")]
        [SerializeField] float mapRadius = 100f;

        [Header("생성 속도")]
        [Tooltip("프레임당 생성할 청크 수. 클수록 빠르지만 프리징 증가.")]
        [SerializeField] int chunksPerFrame = 3;

        // =====================================================================
        // 네트워크 변수
        // =====================================================================

        private readonly NetworkVariable<Vector3> _netSeed = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // =====================================================================
        // 내부 변수
        // =====================================================================

        private float   _chunkWorldSize;
        private Vector3 _sharedSeed;
        private bool    _worldInitialized = false;

        private Dictionary<Vector3Int, MarchingChunk> _activeChunks = new();

        // =====================================================================
        // 공개 프로퍼티
        // =====================================================================

        public Dictionary<Vector3Int, MarchingChunk> ActiveChunks => _activeChunks;
        public float ChunkWorldSize                                => _chunkWorldSize;
        public bool  IsReady                                       { get; private set; }

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>청크 생성 진행률 (0~1). 로딩 UI에서 구독.</summary>
        public static event Action<float> OnLoadingProgress;

        /// <summary>모든 청크 생성 완료. EnemySpawner 등에서 구독해 스폰 시작 신호로 사용.</summary>
        public static event Action OnWorldReady;

        // =====================================================================
        // NGO 생명주기
        // =====================================================================

        public override void OnNetworkSpawn()
        {
            _chunkWorldSize = size * voxelSize;

            if (IsServer)
            {
                Vector3 seed = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value) * 10000f;
                _netSeed.Value = seed;
                InitializeWorld(seed);
            }
            else
            {
                _netSeed.OnValueChanged += OnSeedChanged;
                if (_netSeed.Value.sqrMagnitude > 0.001f)
                    InitializeWorld(_netSeed.Value);
            }

            Debug.Log($"[WorldManager] OnNetworkSpawn — IsServer={IsServer}");
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
            Debug.Log($"[WorldManager] 월드 초기화 — Seed={seed}, MapRadius={mapRadius}");
            StartCoroutine(GenerateAllChunksCoroutine());
        }

        // =====================================================================
        // Update (클라이언트 fallback — seed를 늦게 받은 경우)
        // =====================================================================

        void Update()
        {
            if (!_worldInitialized && !IsServer && _netSeed.Value.sqrMagnitude > 0.001f)
                InitializeWorld(_netSeed.Value);
        }

        // =====================================================================
        // 맵 전체 청크 생성 (코루틴 — 프레임당 chunksPerFrame개씩 생성)
        // =====================================================================

        private IEnumerator GenerateAllChunksCoroutine()
        {
            int   maxRange  = Mathf.CeilToInt(mapRadius / _chunkWorldSize);
            float halfChunk = _chunkWorldSize * 0.5f;

            // 생성할 좌표 목록 먼저 수집
            var coords = new List<Vector3Int>();
            for (int x = -maxRange; x <= maxRange; x++)
            for (int y = -maxRange; y <= maxRange; y++)
            for (int z = -maxRange; z <= maxRange; z++)
            {
                Vector3 chunkCenter = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * _chunkWorldSize;
                if (chunkCenter.magnitude <= mapRadius + halfChunk)
                    coords.Add(new Vector3Int(x, y, z));
            }

            int total     = coords.Count;
            int generated = 0;

            foreach (var coord in coords)
            {
                SpawnChunk(coord);
                generated++;

                OnLoadingProgress?.Invoke((float)generated / total);

                if (generated % chunksPerFrame == 0)
                    yield return null;  // 다음 프레임으로
            }

            IsReady = true;
            Debug.Log($"[WorldManager] 청크 생성 완료 — 총 {_activeChunks.Count}개");
            OnWorldReady?.Invoke();
        }

        private void SpawnChunk(Vector3Int coord)
        {
            if (_activeChunks.ContainsKey(coord)) return;

            var obj = new GameObject($"Chunk_{coord.x}_{coord.y}_{coord.z}");
            obj.transform.SetParent(transform);
            obj.transform.position = new Vector3(coord.x, coord.y, coord.z) * _chunkWorldSize;

            var chunk = obj.AddComponent<MarchingChunk>();
            chunk.SetMaterialMap(voxelMaterialMap);
            chunk.CreateChunk(size, size, size, voxelSize, _sharedSeed, Vector3.zero, mapRadius);

            _activeChunks.Add(coord, chunk);
        }

        // =====================================================================
        // 뷰어 등록 (EnemySpawner 호환성 유지용 — 스트리밍 제거로 no-op)
        // =====================================================================

        public void RegisterViewer(Transform viewer, int chunkDist = 1) { }
        public void UnregisterViewer(Transform viewer) { }

        // =====================================================================
        // 지형 수정 — 네트워크 동기화
        // =====================================================================

        public float[] ModifyTerrain(Vector3 worldPoint, float radius, float strength,
                                     VoxelType placeType = VoxelType.Air)
        {
            if (IsServer)
            {
                float[] result = ApplyModifyLocally(worldPoint, radius, strength, placeType);
                BroadcastModifyClientRpc(worldPoint, radius, strength, (byte)placeType);
                return result;
            }
            else
            {
                float[] result = ApplyModifyLocally(worldPoint, radius, strength, placeType);
                RequestModifyServerRpc(worldPoint, radius, strength, (byte)placeType);
                return result;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestModifyServerRpc(Vector3 worldPoint, float radius, float strength,
                                            byte placeType, ServerRpcParams rpcParams = default)
        {
            ApplyModifyLocally(worldPoint, radius, strength, (VoxelType)placeType);

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

        [ClientRpc]
        private void BroadcastModifyClientRpc(Vector3 worldPoint, float radius, float strength,
                                              byte placeType, ClientRpcParams clientRpcParams = default)
        {
            if (IsServer) return;
            ApplyModifyLocally(worldPoint, radius, strength, (VoxelType)placeType);
        }

        private float[] ApplyModifyLocally(Vector3 worldPoint, float radius, float strength,
                                           VoxelType placeType)
        {
            float   margin         = radius + voxelSize;
            float[] totalDugByType = new float[VoxelDatabase.TypeCount];
            foreach (var kvp in _activeChunks)
            {
                if (kvp.Value.ContainsPoint(worldPoint, margin))
                {
                    float[] chunk = kvp.Value.ModifyDensity(worldPoint, radius, strength, placeType);
                    for (int i = 0; i < VoxelDatabase.TypeCount; i++) totalDugByType[i] += chunk[i];
                }
            }
            return totalDugByType;
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        public Vector3Int WorldToChunkCoord(Vector3 worldPos) => new(
            Mathf.FloorToInt(worldPos.x / _chunkWorldSize),
            Mathf.FloorToInt(worldPos.y / _chunkWorldSize),
            Mathf.FloorToInt(worldPos.z / _chunkWorldSize)
        );
    }
}
