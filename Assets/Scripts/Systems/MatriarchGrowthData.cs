// =============================================================================
// MatriarchGrowthData.cs - 성체 성장 트리 공유 상태 (서버 권위형)
//
// NetworkList로 해제된 노드 ID를 동기화 → 모든 클라이언트가 동일한 상태 확인.
// 구매는 ServerRpc를 통해 서버가 요청자의 인벤토리에서 GE를 차감한다.
// =============================================================================
using System;
using System.IO;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using Newtonsoft.Json;
using BioBreach.Engine;
using BioBreach.Engine.Item;

namespace BioBreach.Systems
{
    public class MatriarchGrowthData : NetworkBehaviour
    {
        // =====================================================================
        // 싱글톤 + 이벤트
        // =====================================================================

        public static MatriarchGrowthData Instance { get; private set; }

        /// <summary>노드 해제 등 상태 변화 시 모든 클라이언트에서 발생</summary>
        public static event Action OnGrowthChanged;

        // =====================================================================
        // 데이터
        // =====================================================================

        static MatriarchNode[] _allNodes;
        public static MatriarchNode[] AllNodes => _allNodes ?? LoadNodes();

        NetworkList<FixedString64Bytes> _unlockedNodeIds;

        // =====================================================================
        // NGO 생명주기
        // =====================================================================

        void Awake()
        {
            _unlockedNodeIds = new NetworkList<FixedString64Bytes>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _unlockedNodeIds.OnListChanged += OnListChanged;
            RebuildCache();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            _unlockedNodeIds.OnListChanged -= OnListChanged;
        }

        void OnListChanged(NetworkListEvent<FixedString64Bytes> _)
        {
            RebuildCache();
            OnGrowthChanged?.Invoke();
        }

        // =====================================================================
        // 쿼리
        // =====================================================================

        public bool IsUnlocked(string id)
        {
            foreach (var n in _unlockedNodeIds)
                if (n.ToString() == id) return true;
            return false;
        }

        public bool CanUnlock(MatriarchNode node)
        {
            if (IsUnlocked(node.id)) return false;
            if (node.prerequisiteIds != null)
                foreach (var pid in node.prerequisiteIds)
                    if (!string.IsNullOrEmpty(pid) && !IsUnlocked(pid)) return false;
            return true;
        }

        // =====================================================================
        // 캐시 갱신 → MatriarchBonusCache에 쓰기
        // =====================================================================

        void RebuildCache()
        {
            MatriarchBonusCache.MaxHpBonus      = SumBonus(MatriarchStatType.MaxHp);
            MatriarchBonusCache.RegenBonus      = SumBonus(MatriarchStatType.Regen);
            MatriarchBonusCache.ArmorBonus      = SumBonus(MatriarchStatType.Armor);
            MatriarchBonusCache.SpawnDelayBonus = SumBonus(MatriarchStatType.SpawnDelay);
            MatriarchBonusCache.WaveRewardBonus = SumBonus(MatriarchStatType.WaveReward);
            MatriarchBonusCache.NotifyChanged();
        }

        float SumBonus(MatriarchStatType t)
        {
            float sum = 0f;
            foreach (var n in AllNodes)
                if (n.ParsedStatType == t && IsUnlocked(n.id)) sum += n.bonusValue;
            return sum;
        }

        // =====================================================================
        // ServerRpc — 클라이언트가 노드 구매 요청
        // =====================================================================

        [ServerRpc(RequireOwnership = false)]
        public void PurchaseNodeServerRpc(string nodeId, ServerRpcParams p = default)
        {
            // 노드 조회
            MatriarchNode node = null;
            foreach (var n in AllNodes)
                if (n.id == nodeId) { node = n; break; }
            if (node == null || !CanUnlock(node)) return;

            // 요청자 인벤토리에서 GE 차감
            ulong senderId = p.Receive.SenderClientId;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var client)) return;

            var inv = client.PlayerObject?.GetComponent<IInventoryContext>()?.Inventory;
            if (inv == null || inv.GetTotalCount("raw_genetic_essence") < node.cost) return;

            inv.RemoveItems("raw_genetic_essence", node.cost);
            _unlockedNodeIds.Add(new FixedString64Bytes(node.id));
        }

        // =====================================================================
        // JSON 로드
        // =====================================================================

        static MatriarchNode[] LoadNodes()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Data", "Progression", "matriarch_nodes.json");
            if (!File.Exists(path)) return _allNodes = new MatriarchNode[0];
            string json = File.ReadAllText(path);
            return _allNodes = JsonConvert.DeserializeObject<MatriarchNode[]>(json) ?? new MatriarchNode[0];
        }
    }
}
