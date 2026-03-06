// =============================================================================
// MatriarchCraftingStation.cs
// 범위 내 플레이어 감지만 담당. UI는 InventoryUI가 인벤토리 창 옆에 렌더링한다.
// =============================================================================
using Unity.Netcode;
using UnityEngine;

namespace BioBreach.Controller.Matriarch
{
    public class MatriarchCraftingStation : MonoBehaviour
    {
        [Header("상호작용 범위")]
        [SerializeField] public float interactRadius = 6f;

        /// <summary>로컬 플레이어가 범위 안에 있으면 true</summary>
        public bool IsLocalPlayerInRange { get; private set; }

        void Update()
        {
            var localObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            IsLocalPlayerInRange = localObj != null &&
                Vector3.Distance(transform.position, localObj.transform.position) <= interactRadius;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
