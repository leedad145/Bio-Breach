// =============================================================================
// EnemySpawner.cs - 적 유닛 스포너
// 일정 간격으로 Enemy를 소환하고, 소환마다 스케일링 가중치를 누적 적용.
// 소환 시 주변 Voxel을 제거해 Enemy가 지형 안에 끼지 않게 함.
// 웨이브 클리어 시 모든 플레이어에게 GE 보상 지급.
// =============================================================================

using System;
using UnityEngine;
using Unity.Netcode;
using VContainer;
using VContainer.Unity;
using BioBreach.Systems;
using BioBreach.Core.Voxel;
using BioBreach.Engine;
using BioBreach.Engine.Data;
using BioBreach.Engine.Item;

namespace BioBreach.Controller.Enemy
{
    [Serializable]
    public struct SpawnEntry
    {
        public GameObject prefab;
        [Min(0f)] public float weight;
    }

    public class EnemySpawner : NetworkBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("소환 설정")]
        [Tooltip("소환할 Enemy 프리팹")]
        public GameObject enemyPrefab;

        [Tooltip("소환 간격 (초) — MatriarchGrowthData의 SpawnDelay 보너스가 추가된다")]
        [Min(0.1f)] public float spawnInterval = 5f;

        [Header("웨이브 당 소환 수")]
        [Tooltip("첫 웨이브에 소환할 마리 수")]
        [Min(1)] public int initialSpawnCount = 1;
        [Tooltip("웨이브마다 소환 수 증가량 (소수 누적).\n" +
                 "0.2 = 5웨이브마다 +1마리, 1 = 매 웨이브 +1마리")]
        [Min(0f)] public float countScalePerWave = 0f;

        [Header("난이도 스케일링 (웨이브마다 누적)")]
        [Min(0f)] public float scalingPerSpawn      = 0.1f;
        [Min(0f)] public float speedScalingPerSpawn = 0.05f;

        [Header("소환 범위 (Matriarch 기준)")]
        [Min(0f)] public float spawnRadius    = 100f;
        [Min(0f)] public float minSpawnRadius = 20f;

        [Header("소환 위치 Voxel 제거")]
        [Min(0f)] public float spawnClearRadius   = 5f;
        [Min(0f)] public float spawnClearStrength = 1f;

        [Header("웨이브 클리어 보상 (GE)")]
        [Tooltip("기본 보상 GE 수량")]
        [Min(0)] public int rewardBase = 2;
        [Tooltip("웨이브당 보상 증가 (소수 누적). 0.5 = 2웨이브마다 +1개")]
        [Min(0f)] public float rewardScaling = 0.5f;

        [Header("소환 풀 (비어있으면 enemyPrefab 사용)")]
        [Tooltip("가중치 기반 랜덤 소환. 비어 있으면 enemyPrefab 단독 사용.")]
        public SpawnEntry[] spawnPool;

        [Header("참조")]
        public WorldManager worldManager;

        // =====================================================================
        // 네트워크 변수 (HUD용)
        // =====================================================================

        private readonly NetworkVariable<int> _waveNumber = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _nextWaveTime = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // HUD에서 읽는 공개 프로퍼티
        public int   WaveNumber             => _waveNumber.Value;
        public float NextWaveTime           => _nextWaveTime.Value;
        public float SpawnInterval          => spawnInterval;
        public float CurrentSpawnDelayBonus => MatriarchBonusCache.SpawnDelayBonus;

        // =====================================================================
        // 이벤트
        // =====================================================================

        /// <summary>웨이브 클리어 시 발생 (WaveHUD 알림용). 파라미터: 클리어된 웨이브 인덱스</summary>
        public static event Action<int> OnWaveCleared;

        // =====================================================================
        // 내부 변수
        // =====================================================================

        int             _waveIndex;      // 지금까지 진행한 웨이브 수
        float           _timer;
        int             _activeEnemies;  // 현재 살아있는 이번 웨이브 적 수 (Server only)
        IObjectResolver _resolver;
        ItemRepository  _itemRepo;

        [Inject]
        public void Construct(IObjectResolver resolver, ItemRepository itemRepo)
        {
            _resolver = resolver;
            _itemRepo = itemRepo;
        }

        // =====================================================================
        // 초기화
        // =====================================================================

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            if (worldManager == null)
                worldManager = FindAnyObjectByType<WorldManager>();

            // 첫 소환은 지연 없이 바로 실행
            _timer = spawnInterval;
            _nextWaveTime.Value = Time.time;
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            if (!IsServer) return;

            _timer += Time.deltaTime;
            float effectiveInterval = spawnInterval + MatriarchBonusCache.SpawnDelayBonus;
            if (_timer >= effectiveInterval)
            {
                _timer = 0f;
                SpawnWave();
            }
        }

        // =====================================================================
        // 소환 로직
        // =====================================================================

        void SpawnWave()
        {
            if (enemyPrefab == null) return;

            int count = Mathf.Max(1, initialSpawnCount + Mathf.FloorToInt(countScalePerWave * _waveIndex));

            float scale      = 1f + scalingPerSpawn      * _waveIndex;
            float speedScale = 1f + speedScalingPerSpawn * _waveIndex;

            _activeEnemies = count;
            _waveNumber.Value = _waveIndex;

            for (int i = 0; i < count; i++)
                SpawnEnemy(scale, speedScale);

            // 다음 웨이브 예상 시간 갱신
            float next = spawnInterval + MatriarchBonusCache.SpawnDelayBonus;
            _nextWaveTime.Value = Time.time + next;

            _waveIndex++;
        }

        GameObject PickPrefab()
        {
            if (spawnPool == null || spawnPool.Length == 0) return enemyPrefab;

            float total = 0f;
            foreach (var e in spawnPool) total += e.weight;
            if (total <= 0f) return enemyPrefab;

            float r = UnityEngine.Random.Range(0f, total);
            float cum = 0f;
            foreach (var e in spawnPool)
            {
                cum += e.weight;
                if (r < cum) return e.prefab;
            }
            return spawnPool[^1].prefab;
        }

        void SpawnEnemy(float scale, float speedScale)
        {
            Vector3 spawnPos = GetRandomSpawnPosition();

            if (worldManager != null && spawnClearRadius > 0f)
                worldManager.ModifyTerrain(spawnPos, spawnClearRadius, spawnClearStrength, VoxelType.Air);

            var prefab = PickPrefab();
            if (prefab == null) return;
            var go = Instantiate(prefab, spawnPos, transform.rotation);
            _resolver?.InjectGameObject(go);

            var enemy = go.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.hpMultiplier     = scale;
                enemy.damageMultiplier = scale;
                enemy.speedMultiplier  = speedScale;
                enemy.homeSpawner      = this;

                if (worldManager != null) enemy.worldManager = worldManager;
                enemy.matriarchTarget = transform;
            }

            if (go.TryGetComponent<NetworkObject>(out var netObj))
                netObj.Spawn(destroyWithScene: true);
            else
                Debug.LogWarning($"[EnemySpawner] {enemyPrefab.name}에 NetworkObject 컴포넌트가 없습니다.");
        }

        // =====================================================================
        // 웨이브 클리어 처리 (EnemyController에서 호출)
        // =====================================================================

        /// <summary>EnemyController가 사망 시 서버에서 호출. 웨이브 클리어를 판정한다.</summary>
        public void ReportEnemyDied()
        {
            if (!IsServer) return;
            _activeEnemies = Mathf.Max(0, _activeEnemies - 1);
            if (_activeEnemies == 0 && _waveIndex > 0)
                WaveClearedInternal(_waveIndex - 1);
        }

        void WaveClearedInternal(int clearedWave)
        {
            // 보상 계산 (MatriarchGrowthData WaveReward 보너스 포함)
            int reward = Mathf.Max(1, rewardBase
                                    + Mathf.FloorToInt(rewardScaling * clearedWave)
                                    + Mathf.FloorToInt(MatriarchBonusCache.WaveRewardBonus));

            // 모든 접속 플레이어에게 GE 지급
            var geItem = _itemRepo?.CreateItem("raw_genetic_essence");
            if (geItem != null)
            {
                foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
                {
                    var inv = client.PlayerObject?.GetComponent<IInventoryContext>()?.Inventory;
                    inv?.TryAddItem(geItem, reward);
                }
            }

            // HUD 알림 이벤트 (모든 클라이언트)
            NotifyWaveClearedClientRpc(clearedWave);
        }

        [ClientRpc]
        void NotifyWaveClearedClientRpc(int clearedWave)
        {
            OnWaveCleared?.Invoke(clearedWave);
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        Vector3 GetRandomSpawnPosition()
        {
            float   angle  = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float   dist   = UnityEngine.Random.Range(minSpawnRadius, spawnRadius);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            return transform.position + offset;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, minSpawnRadius);
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, spawnClearRadius);
        }
    }
}
