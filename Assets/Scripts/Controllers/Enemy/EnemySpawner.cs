// =============================================================================
// EnemySpawner.cs - 적 유닛 스포너
// 일정 간격으로 Enemy를 소환하고, 소환마다 스케일링 가중치를 누적 적용.
// 소환 시 주변 Voxel을 제거해 Enemy가 지형 안에 끼지 않게 함.
// =============================================================================

using UnityEngine;
using Unity.Netcode;
using VContainer;
using VContainer.Unity;
using BioBreach.Systems;
using BioBreach.Core.Voxel;
using BioBreach.Controller.Enemy;

namespace BioBreach.Controller.Enemy
{
    /// <summary>
    /// Enemy 스포너.
    /// <list type="bullet">
    ///   <item>소환 프리팹, 소환 간격, 스케일링 가중치 설정 가능</item>
    ///   <item>소환마다 HP·공격력·이속에 누적 가중치 적용</item>
    ///   <item>소환 위치 주변 Voxel 제거 (범위·강도 설정 가능)</item>
    /// </list>
    /// </summary>
    public class EnemySpawner : NetworkBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("소환 설정")]
        [Tooltip("소환할 Enemy 프리팹")]
        public GameObject enemyPrefab;

        [Tooltip("소환 간격 (초)")]
        [Min(0.1f)] public float spawnInterval = 5f;

        [Header("난이도 스케일링 (소환 횟수마다 누적)")]
        [Tooltip("소환마다 HP·공격력에 적용되는 배율 증가량.\n" +
                 "0 = 스케일 없음, 0.1 = 소환마다 10% 증가.\n" +
                 "최종 배율 = 1 + scalingPerSpawn × 소환횟수")]
        [Min(0f)] public float scalingPerSpawn = 0.1f;

        [Tooltip("이속 스케일링 배율 (HP·공격력보다 느리게 증가시키려면 낮게 설정)")]
        [Min(0f)] public float speedScalingPerSpawn = 0.05f;

        [Header("소환 위치 Voxel 제거")]
        [Tooltip("소환 시 주변을 파낼 구의 반경 (0이면 파기 안 함)")]
        [Min(0f)] public float spawnClearRadius   = 5f;
        [Tooltip("Voxel 제거 강도")]
        [Min(0f)] public float spawnClearStrength = 1f;

        [Header("참조")]
        [Tooltip("WorldManager (null이면 자동 탐색)")]
        public WorldManager worldManager;
        [Tooltip("성체 Transform — 소환된 Enemy에 자동 주입")]
        public Transform matriarchTarget;

        // =====================================================================
        // 내부 변수
        // =====================================================================

        int              _spawnCount;
        float            _timer;
        IObjectResolver  _resolver;

        [Inject]
        public void Construct(IObjectResolver resolver) => _resolver = resolver;

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
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            // 소환은 Server에서만 실행
            if (!IsServer) return;

            _timer += Time.deltaTime;
            if (_timer >= spawnInterval)
            {
                _timer = 0f;
                SpawnEnemy();
            }
        }

        // =====================================================================
        // 소환 로직
        // =====================================================================

        void SpawnEnemy()
        {
            if (enemyPrefab == null) return;

            // 1. 소환 위치 주변 Voxel 제거
            if (worldManager != null && spawnClearRadius > 0f)
                worldManager.ModifyTerrain(transform.position, spawnClearRadius, spawnClearStrength, VoxelType.Air);

            // 2. Enemy 생성 (Server) + VContainer 의존성 주입
            var go = Instantiate(enemyPrefab, transform.position, transform.rotation);
            _resolver?.InjectGameObject(go);

            // 3. 스케일링 주입 — NetworkSpawn 이전(Start 이전)에 반영되어야 maxHp에 적용됨
            var enemy = go.GetComponent<EnemyController>();
            if (enemy != null)
            {
                float scale      = 1f + scalingPerSpawn      * _spawnCount;
                float speedScale = 1f + speedScalingPerSpawn * _spawnCount;

                enemy.hpMultiplier     = scale;
                enemy.damageMultiplier = scale;
                enemy.speedMultiplier  = speedScale;

                if (matriarchTarget != null) enemy.matriarchTarget = matriarchTarget;
                if (worldManager    != null) enemy.worldManager    = worldManager;
            }

            // 4. 네트워크에 스폰 — 모든 클라이언트에 오브젝트 생성
            if (go.TryGetComponent<NetworkObject>(out var netObj))
                netObj.Spawn(destroyWithScene: true);
            else
                Debug.LogWarning($"[EnemySpawner] {enemyPrefab.name}에 NetworkObject 컴포넌트가 없습니다.");

            _spawnCount++;
        }

        // =====================================================================
        // 기즈모
        // =====================================================================

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, spawnClearRadius);
        }
    }
}
