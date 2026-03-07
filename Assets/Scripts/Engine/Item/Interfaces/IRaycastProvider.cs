// =============================================================================
// IRaycastProvider.cs - 레이캐스트 결과 + 공격 원점 (ISP: 레이캐스트만 담당)
// =============================================================================
using UnityEngine;

namespace BioBreach.Engine.Item
{
    /// <summary>카메라 정면 레이캐스트 캐시 및 공격 파라미터.</summary>
    public interface IRaycastProvider
    {
        bool       HasHit         { get; }
        RaycastHit Hit            { get; }
        Vector3    AttackOrigin   { get; }
        Vector3    AttackDirection{ get; }
    }
}
