// =============================================================================
// NavMeshRebakeManager.cs
// NavMeshSurface를 월드 준비 직후 베이크하고, 이후 주기적으로 재베이크한다.
// 다이나믹 Voxel 지형이 파괴될 때 NavMesh를 갱신하기 위해 필요하다.
//
// 사용법: 씬 빈 GameObject에 이 컴포넌트 + NavMeshSurface 컴포넌트 추가.
//         NavMeshSurface의 Collect Objects = All Game Objects,
//         Use Geometry = Physics Colliders 설정 권장.
// =============================================================================

using Unity.AI.Navigation;
using UnityEngine;
using BioBreach.Systems;

namespace BioBreach.Controller.Enemy
{
    public class NavMeshRebakeManager : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("NavMeshSurface 컴포넌트. 없으면 자동 탐색.")]
        public NavMeshSurface surface;

        [Header("재베이크 간격 (초)")]
        [Tooltip("Voxel 지형이 변경되면 이 간격마다 NavMesh를 재빌드한다.\n" +
                 "너무 짧으면 CPU 부하, 너무 길면 적이 파낸 경로를 인식 못 함.")]
        [Min(1f)] public float rebakeInterval = 5f;

        bool  _ready;
        float _rebakeTimer;

        // =====================================================================
        // 초기화
        // =====================================================================

        void OnEnable()
        {
            if (surface == null) surface = FindAnyObjectByType<NavMeshSurface>();
            WorldManager.OnWorldReady += OnWorldReady;
        }

        void OnDisable()
        {
            WorldManager.OnWorldReady -= OnWorldReady;
        }

        void OnWorldReady()
        {
            surface?.BuildNavMesh();
            _ready = true;
            _rebakeTimer = 0f;
        }

        // =====================================================================
        // 주기적 재베이크
        // =====================================================================

        void Update()
        {
            if (!_ready || surface == null) return;

            _rebakeTimer += Time.deltaTime;
            if (_rebakeTimer >= rebakeInterval)
            {
                _rebakeTimer = 0f;
                surface.BuildNavMesh();
            }
        }

        /// <summary>
        /// 외부에서 즉시 재베이크가 필요할 때 호출 (예: 대규모 지형 파괴 후).
        /// </summary>
        public void RequestImmediateRebake()
        {
            if (surface == null) return;
            surface.BuildNavMesh();
            _rebakeTimer = 0f;
        }
    }
}
