// =============================================================================
// NavMeshRebakeManager.cs
// Dig 등으로 지형이 변경됐을 때만 NavMesh를 재베이크한다.
//
// 최적화 전략:
//   - 매 N초 무조건 재베이크 → dirty flag가 설정됐을 때만 재베이크
//   - MarkDirty()는 EnemyController.TryDigForward에서 호출
//   - 최소 재베이크 간격(rebakeInterval)을 지켜 프레임 드랍 방지
//   - BuildNavMesh()는 메인 스레드에서 동기 실행 → 간격을 충분히 크게 설정 권장
//
// 씬 설치:
//   빈 GameObject에 이 컴포넌트 + NavMeshSurface를 추가.
//   NavMeshSurface: Collect Objects = All Game Objects,
//                   Use Geometry = Physics Colliders
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

        [Header("재베이크 최소 간격 (초)")]
        [Tooltip("Dig 등으로 dirty가 됐을 때 이 간격이 지난 후에만 재베이크.\n" +
                 "너무 짧으면 BuildNavMesh() 오버헤드로 프레임 드랍.\n" +
                 "권장: 3~8 초.")]
        [Min(0.5f)] public float rebakeInterval = 5f;

        bool  _ready;
        bool  _isDirty;
        float _lastBakeTime = float.NegativeInfinity;

        // =====================================================================
        // 생명주기
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
            if (surface == null) return;
            surface.BuildNavMesh();
            _lastBakeTime = Time.time;
            _ready = true;
        }

        // =====================================================================
        // 업데이트 — dirty 시에만 재베이크
        // =====================================================================

        void Update()
        {
            if (!_ready || !_isDirty || surface == null) return;
            if (Time.time - _lastBakeTime < rebakeInterval) return;

            surface.BuildNavMesh();
            _lastBakeTime = Time.time;
            _isDirty = false;
        }

        // =====================================================================
        // 공개 API
        // =====================================================================

        /// <summary>
        /// 지형이 변경됐음을 알린다.
        /// 다음 rebakeInterval 경과 후 재베이크가 실행된다.
        /// EnemyController.TryDigForward에서 Dig 후 호출.
        /// </summary>
        public void MarkDirty() => _isDirty = true;

        /// <summary>
        /// 즉시 재베이크 (간격 무시). 대규모 지형 파괴 직후 등에 사용.
        /// </summary>
        public void RequestImmediateRebake()
        {
            if (surface == null) return;
            surface.BuildNavMesh();
            _lastBakeTime = Time.time;
            _isDirty = false;
        }
    }
}
