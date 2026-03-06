// =============================================================================
// DroppedItem.cs
// 월드에 떨어진 아이템 NetworkBehaviour.
// 서버에서 Spawn 후 Init() 호출 → 근처 플레이어가 F 키로 습득.
// ★ Unity Editor에서 NetworkManager.NetworkPrefabs 목록에 반드시 등록할 것.
// =============================================================================
using Unity.Netcode;
using UnityEngine;
using BioBreach.Engine.Data;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    public class DroppedItem : NetworkBehaviour
    {
        private const float PickupRadius = 2.5f;

        // 서버→클라이언트 동기화용 로컬 필드 (NetworkVariable<string> 대신 RPC로 초기화)
        private string _itemId = "";
        private int    _count  = 1;

        // GUIStyle 캐시
        private GUIStyle _labelStyle;

        // =====================================================================
        // 서버 초기화 (Spawn 직후 서버에서 호출)
        // =====================================================================

        /// <summary>서버에서 Spawn 후 즉시 호출해 아이템 데이터를 모든 클라이언트에 동기화한다.</summary>
        public void Init(string itemId, int count)
        {
            _itemId = itemId;
            _count  = count;
            SyncDataClientRpc(itemId, count);
        }

        [ClientRpc]
        private void SyncDataClientRpc(string itemId, int count)
        {
            _itemId = itemId;
            _count  = count;
        }

        // =====================================================================
        // 클라이언트 Update — F 키 습득
        // =====================================================================

        void Update()
        {
            if (!IsSpawned || string.IsNullOrEmpty(_itemId)) return;

            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (localPlayer == null) return;

            float dist = Vector3.Distance(transform.position, localPlayer.transform.position);
            if (dist <= PickupRadius && Input.GetKeyDown(KeyCode.F))
                PickUpServerRpc();
        }

        // =====================================================================
        // OnGUI — 범위 안일 때 "[F] itemName ×N" 라벨 표시
        // =====================================================================

        void OnGUI()
        {
            if (!IsSpawned || string.IsNullOrEmpty(_itemId)) return;

            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (localPlayer == null) return;

            float dist = Vector3.Distance(transform.position, localPlayer.transform.position);
            if (dist > PickupRadius) return;

            string name = GameDataLoader.Items.TryGet(_itemId, out var d) ? d.itemName : _itemId;

            var cam = Camera.main;
            if (cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 0.6f);
            if (screen.z < 0f) return;

            float sx = screen.x;
            float sy = Screen.height - screen.y;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize   = 12,
                    fontStyle  = FontStyle.Bold
                };
                _labelStyle.normal.textColor = Color.yellow;
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(sx - 90f, sy - 22f, 180f, 22f), $"[F]  {name}  ×{_count}", _labelStyle);
        }

        // =====================================================================
        // 습득 ServerRpc
        // =====================================================================

        [ServerRpc(RequireOwnership = false)]
        private void PickUpServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsSpawned) return;

            ulong senderId = rpcParams.Receive.SenderClientId;

            var p = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } }
            };
            GrantItemClientRpc(_itemId, _count, p);

            NetworkObject.Despawn(destroy: true);
        }

        // =====================================================================
        // 아이템 지급 ClientRpc (요청한 클라이언트 한 명에게만 전송됨)
        // =====================================================================

        [ClientRpc]
        private void GrantItemClientRpc(string itemId, int count,
            ClientRpcParams clientRpcParams = default)
        {
            GameDataLoader.EnsureLoaded();

            var item = GameDataLoader.CreateItemSO(itemId);
            if (item == null) return;

            var inv = NetworkManager.Singleton?.LocalClient?.PlayerObject
                          ?.GetComponent<PlayerInventory>();
            if (inv == null) return;

            inv.TryAddItem(item, count);
        }
    }
}
