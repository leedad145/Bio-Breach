// =============================================================================
// DroppedItem.cs
// 월드에 떨어진 아이템 NetworkBehaviour.
// 서버에서 Spawn 후 Init() 호출 → 플레이어가 아이템을 조준(크로스헤어)할 때 F 키로 습득.
// ★ Unity Editor에서 NetworkManager.NetworkPrefabs 목록에 반드시 등록할 것.
// ★ 프리팹에 NetworkTransform 컴포넌트 추가 권장 (위치 동기화).
// =============================================================================
using Unity.Netcode;
using UnityEngine;
using BioBreach.Engine.Data;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    public class DroppedItem : NetworkBehaviour
    {
        private const float PickupMaxDist  = 5f;   // 습득 가능 최대 거리
        private const float AutoPickupDist = 1.5f; // 이 거리 이하면 자동 흡수

        private string _itemId = "";
        private int    _count  = 1;

        // 중력
        private Rigidbody _rb;

        // 조준 캐시 (Update에서 계산, OnGUI에서 사용)
        private bool _isLookedAt = false;

        // GUIStyle 캐시
        private GUIStyle _labelStyle;

        // =====================================================================
        // Awake — Rigidbody·Collider 보장
        // =====================================================================

        void Awake()
        {
            // Rigidbody 없으면 추가 (중력)
            if (!TryGetComponent(out _rb))
            {
                _rb = gameObject.AddComponent<Rigidbody>();
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            _rb.freezeRotation = true;

            // Collider 없으면 추가 (레이캐스트 감지)
            if (!TryGetComponent<Collider>(out _))
            {
                var sc = gameObject.AddComponent<SphereCollider>();
                sc.radius = 0.25f;
            }
        }

        // =====================================================================
        // 서버 초기화 (Spawn 직후 서버에서 호출)
        // =====================================================================

        /// <summary>서버에서 Spawn 후 즉시 호출해 아이템 데이터를 모든 클라이언트에 동기화한다.</summary>
        public void Init(string itemId, int count)
        {
            _itemId = itemId;
            _count  = count;
            SyncDataClientRpc(itemId, count);
            if (IsServer) Invoke(nameof(TryMergeWithNearby), 0.5f);
        }

        private const float MergeRadius = 3f;

        private void TryMergeWithNearby()
        {
            if (!IsServer || !IsSpawned || _count <= 0) return;

            GameDataLoader.EnsureLoaded();
            if (!GameDataLoader.Items.TryGet(_itemId, out var data)) return;
            int maxStack = data.maxStack;

            var cols = Physics.OverlapSphere(transform.position, MergeRadius);
            foreach (var col in cols)
            {
                if (col.gameObject == gameObject) continue;
                var other = col.GetComponent<DroppedItem>();
                if (other == null || !other.IsSpawned || other._itemId != _itemId) continue;
                if (_count + other._count > maxStack) continue;

                _count += other._count;
                SyncDataClientRpc(_itemId, _count);
                other.NetworkObject.Despawn(destroy: true);
                break;
            }
        }

        [ClientRpc]
        private void SyncDataClientRpc(string itemId, int count)
        {
            _itemId = itemId;
            _count  = count;
        }

        // =====================================================================
        // Update — 조준 판정 캐시 & F 키 습득
        // =====================================================================

        void Update()
        {
            if (!IsSpawned || string.IsNullOrEmpty(_itemId)) return;

            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (localPlayer == null) { _isLookedAt = false; return; }

            float dist = Vector3.Distance(transform.position, localPlayer.transform.position);
            if (dist > PickupMaxDist)
            {
                _isLookedAt = false;
                return;
            }

            // 근접 시 자동 흡수
            if (dist <= AutoPickupDist)
            {
                _isLookedAt = false;
                PickUpServerRpc();
                return;
            }

            _isLookedAt = CheckLookedAt();

            if (_isLookedAt && Input.GetKeyDown(KeyCode.F))
                PickUpServerRpc();
        }

        // =====================================================================
        // 조준 판정 — 카메라 레이가 이 아이템을 때리는지 확인
        // =====================================================================

        private bool CheckLookedAt()
        {
            var cam = Camera.main;
            if (cam == null) return false;

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, PickupMaxDist))
                return hit.transform == transform || hit.transform.IsChildOf(transform);

            return false;
        }

        // =====================================================================
        // OnGUI — 조준 중일 때 "[F] itemName ×N" 팝업 표시
        // =====================================================================

        void OnGUI()
        {
            if (!IsSpawned || string.IsNullOrEmpty(_itemId)) return;
            if (!_isLookedAt) return;

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
                    fontSize   = 14,
                    fontStyle  = FontStyle.Bold
                };
                _labelStyle.normal.textColor = Color.yellow;
            }

            // 팝업 배경
            float pw = 200f, ph = 28f;
            Rect bg = new Rect(sx - pw * 0.5f - 4f, sy - ph * 0.5f - 4f, pw + 8f, ph + 8f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(sx - pw * 0.5f, sy - ph * 0.5f, pw, ph),
                      $"[F]  {name}  ×{_count}", _labelStyle);
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
