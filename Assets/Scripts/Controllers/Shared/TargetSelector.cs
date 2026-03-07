// =============================================================================
// TargetSelector.cs - 공통 타겟 선택 로직 (Enemy / Turret 공유)
// =============================================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BioBreach.Engine.Entity;

namespace BioBreach.Controller.Shared
{
    public static class TargetSelector
    {
        /// <summary>
        /// <paramref name="origin"/> 주변 <paramref name="range"/> 안의
        /// <paramref name="layer"/> 레이어에서 살아 있는 EntityMonoBehaviour 중
        /// <paramref name="priority"/>에 따라 하나를 반환한다.
        /// 후보 없으면 null.
        /// </summary>
        public static EntityMonoBehaviour FindTarget(
            Vector3        origin,
            float          range,
            LayerMask      layer,
            TargetPriority priority)
        {
            Collider[] hits = Physics.OverlapSphere(origin, range, layer);

            var candidates = new List<EntityMonoBehaviour>(hits.Length);
            foreach (var hit in hits)
            {
                var entity = hit.GetComponent<EntityMonoBehaviour>();
                if (entity != null && entity.IsAlive)
                    candidates.Add(entity);
            }

            if (candidates.Count == 0) return null;

            return priority switch
            {
                TargetPriority.LowestHp       => candidates.OrderBy(e => e.CurrentHp).First(),
                TargetPriority.HighestPriority => candidates.OrderByDescending(e => e.priorityScore).First(),
                _                              => candidates.OrderBy(e =>
                                                     (origin - e.transform.position).sqrMagnitude).First()
            };
        }
    }
}
