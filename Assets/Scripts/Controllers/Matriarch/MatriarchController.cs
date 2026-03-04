// =============================================================================
// MatriarchController.cs - 성체 컨트롤러
// Enemy 유닛들의 최종 이동 목표이자 방어 대상.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;
using BioBreach.Engine.Entity;

namespace BioBreach.Controller.Matriarch
{
    /// <summary>
    /// 성체 — Enemy 유닛들이 최종적으로 공격하는 목표 오브젝트.
    /// HP를 가지며, 파괴되면 onMatriarchDestroyed 이벤트를 발생시킨다.
    /// </summary>
    public class MatriarchController : EntityMonoBehaviour
    {
        [Header("성체 이벤트")]
        [Tooltip("HP가 0이 되면 호출됨 (게임 오버 처리 등 연결)")]
        public UnityEvent onMatriarchDestroyed;

        protected override void HandleDeath()
        {
            onMatriarchDestroyed?.Invoke();
            base.HandleDeath();
        }
    }
}
