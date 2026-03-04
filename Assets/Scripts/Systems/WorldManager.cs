// =============================================================================
// WorldManager.cs - 전체 월드 청크 관리
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using BioBreach.Engine.MarchingCubes;
using BioBreach.Engine.Voxel;
using BioBreach.Core.Voxel;


namespace BioBreach.Systems
{
    /// <summary>
    /// 청크들을 생성/삭제하고 관리하는 매니저
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================
        
        [Header("플레이어")]
        [SerializeField] Transform player;

        [Header("청크 설정")]
        [SerializeField] VoxelMaterialMap voxelMaterialMap;
        [SerializeField] int viewDistance = 5;
        [SerializeField] int size = 20;
        [SerializeField] float voxelSize = 3f;


        // =====================================================================
        // 내부 변수
        // =====================================================================
        
        private float _chunkWorldSize;
        private Vector3 _sharedSeed;
        
        private Dictionary<Vector3Int, MarchingChunk> _activeChunks = new();
        private Stack<MarchingChunk> _chunkPool = new();
        private Vector3Int _lastPlayerChunkPos = new(-999, -999, -999);

        private Queue<Vector3Int> _chunksToGenerate = new();
        private int _maxChunksPerFrame = 2;

        // =====================================================================
        // 공개 프로퍼티
        // =====================================================================
        
        public Dictionary<Vector3Int, MarchingChunk> ActiveChunks => _activeChunks;
        public float ChunkWorldSize => _chunkWorldSize;

        // =====================================================================
        // 초기화
        // =====================================================================
        
        void Start()
        {
            _sharedSeed = new Vector3(Random.value, Random.value, Random.value) * 10000f;
            _chunkWorldSize = size * voxelSize;

            int Side = viewDistance * 2 + 1;
            int poolSize = Side * Side * Side + 30;

            for (int i = 0; i < poolSize; i++)
            {
                MarchingChunk mc = CreatePooledChunk();
                _chunkPool.Push(mc);
            }

            Debug.Log($"[WorldManager] 초기화 완료: 풀 {poolSize}개, 청크 크기 {_chunkWorldSize}m");
        }

        MarchingChunk CreatePooledChunk()
        {
            GameObject obj = new GameObject("Chunk_Pooled");
            obj.SetActive(false);
            obj.transform.SetParent(transform);
            
            MarchingChunk mc = obj.AddComponent<MarchingChunk>();
            return mc;
        }

        // =====================================================================
        // 업데이트
        // =====================================================================
        
        void Update()
        {
            if (player == null) return;

            Vector3Int currentPlayerChunkPos = WorldToChunkPos(player.position);

            if (currentPlayerChunkPos != _lastPlayerChunkPos)
            {
                UpdateChunks(currentPlayerChunkPos);
                _lastPlayerChunkPos = currentPlayerChunkPos;
            }

            ProcessGenerationQueue();
        }

        Vector3Int WorldToChunkPos(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / _chunkWorldSize),
                Mathf.FloorToInt(worldPos.y / _chunkWorldSize),
                Mathf.FloorToInt(worldPos.z / _chunkWorldSize)
            );
        }

        void UpdateChunks(Vector3Int playerPos)
        {
            List<Vector3Int> keysToRemove = new List<Vector3Int>();
            foreach (var coord in _activeChunks.Keys)
            {
                int dx = Mathf.Abs(coord.x - playerPos.x);
                int dz = Mathf.Abs(coord.z - playerPos.z);
                
                if (dx > viewDistance + 1 || dz > viewDistance + 1)
                    keysToRemove.Add(coord);
            }

            foreach (var key in keysToRemove)
            {
                ReturnToPool(_activeChunks[key]);
                _activeChunks.Remove(key);
            }

            _chunksToGenerate.Clear();

            List<(Vector3Int coord, int dist)> toAdd = new();

            for (int x = playerPos.x - viewDistance; x <= playerPos.x + viewDistance; x++)
            for (int y = playerPos.y - viewDistance; y <= playerPos.y + viewDistance; y++)
            for (int z = playerPos.z - viewDistance; z <= playerPos.z + viewDistance; z++)
            {
                Vector3Int coord = new Vector3Int(x, y, z);
                
                if (_activeChunks.ContainsKey(coord)) continue;

                int dist = Mathf.Abs(x - playerPos.x) + 
                           Mathf.Abs(y - playerPos.y) + 
                           Mathf.Abs(z - playerPos.z);
                toAdd.Add((coord, dist));
            }

            toAdd.Sort((a, b) => a.dist.CompareTo(b.dist));
            foreach (var item in toAdd)
                _chunksToGenerate.Enqueue(item.coord);
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

        void ReturnToPool(MarchingChunk chunk)
        {
            chunk.gameObject.SetActive(false);
            chunk.gameObject.name = "Chunk_Pooled";
            chunk.ClearDensityData();
            
            MeshFilter mf = chunk.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh) mf.sharedMesh.Clear();
            
            MeshCollider mc = chunk.GetComponent<MeshCollider>();
            if (mc) mc.sharedMesh = null;
            
            _chunkPool.Push(chunk);
        }

        // =====================================================================
        // 공개 메서드: 지형 수정
        // =====================================================================
        
        /// <summary>
        /// 지형 수정 (파기/설치).
        /// 파기 시 실제 제거된 고체 밀도 합계(≥0)를 반환 — 설치나 변화 없을 땐 0.
        /// </summary>
        public float ModifyTerrain(Vector3 worldPoint, float radius, float strength, VoxelType placeType = VoxelType.Air)
        {
            float margin    = radius + voxelSize;
            float totalDug  = 0f;

            foreach (var kvp in _activeChunks)
            {
                MarchingChunk chunk = kvp.Value;
                if (chunk.ContainsPoint(worldPoint, margin))
                    totalDug += chunk.ModifyDensity(worldPoint, radius, strength, placeType);
            }

            return totalDug;
        }

        public Vector3Int WorldToChunkCoord(Vector3 worldPos)
        {
            return WorldToChunkPos(worldPos);
        }

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
