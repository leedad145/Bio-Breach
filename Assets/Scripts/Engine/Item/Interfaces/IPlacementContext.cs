// =============================================================================
// IPlacementContext.cs - 오브젝트 설치 컨텍스트 (ISP: 설치만 담당)
// =============================================================================
using UnityEngine;

namespace BioBreach.Engine.Item
{
    /// <summary>PlaceableItem이 오브젝트를 배치할 때 필요한 컨텍스트.</summary>
    public interface IPlacementContext
    {
        float PlaceNormalOffset { get; }
        bool  CanPlaceAt(Vector3 pos);
        void  SpawnObject(GameObject prefab, Vector3 pos, Quaternion rot);
    }
}
