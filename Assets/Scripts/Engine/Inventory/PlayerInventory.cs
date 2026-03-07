// =============================================================================
// PlayerInventory.cs - 그리드 기반 플레이어 인벤토리
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using BioBreach.Engine.Item;
using BioBreach.Engine.Data;

namespace BioBreach.Engine.Inventory
{
    [Serializable]
    public class StartItemEntry
    {
        public string id;
        [Min(1)] public int count = 1;
    }

    /// <summary>
    /// 플레이어 인벤토리 (디아블로2식 그리드)
    /// - 그리드 데이터: InventoryGrid
    /// - 핫바: 별도 슬롯 (빠른 접근용)
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("그리드 크기")]
        public int gridColumns = 10;
        public int gridRows    = 6;

        [Header("핫바 슬롯 수")]
        public int hotbarSize = 5;

        [Header("초기 아이템 (JSON id 기반)")]
        public List<StartItemEntry> startItems = new List<StartItemEntry>();

        // =====================================================================
        // 데이터
        // =====================================================================

        private InventoryGrid _grid;

        // 핫바: 그리드의 ItemInstance를 참조 (null이면 빈 슬롯)
        private ItemInstance[] _hotbar;
        private int _selectedHotbarSlot = 0;

        // 장착 슬롯 (5부위) — 아이템은 그리드에도 유지되고 여기에도 참조됨
        private readonly ItemInstance[] _equips = new ItemInstance[5];

        /// <summary>장착 슬롯 변경 시 발행. PlayerController가 구독해 스탯을 재계산한다.</summary>
        public event Action OnEquipmentChanged;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public InventoryGrid Grid          => _grid;
        public ItemInstance[] Hotbar       => _hotbar;
        public int SelectedSlotIndex       => _selectedHotbarSlot;
        public ItemInstance SelectedItem   => _hotbar[_selectedHotbarSlot];

        /// <summary>해당 부위에 현재 장착된 ItemInstance. 비어 있으면 null.</summary>
        public ItemInstance GetEquipped(Item.EquipSlot slot) => _equips[(int)slot];

        // =====================================================================
        // 초기화
        // =====================================================================
        
        void Awake()
        {
            _grid   = new InventoryGrid(gridColumns, gridRows);
            _hotbar = new ItemInstance[hotbarSize];
        }

        void Start()
        {
            GameDataLoader.EnsureLoaded();

            foreach (var entry in startItems)
            {
                if (string.IsNullOrEmpty(entry.id)) continue;
                var so = GameDataLoader.CreateItemSO(entry.id);
                if (so != null)
                    TryAddItem(so, entry.count);
            }
        }

        // =====================================================================
        // 아이템 추가 / 제거
        // =====================================================================

        /// <summary>
        /// 아이템을 그리드에 자동 배치 (성공 여부 반환)
        /// </summary>
        public bool TryAddItem(Item.ItemBase data, int count = 1)
        {
            return _grid.TryAddAuto(data, count);
        }

        /// <summary>
        /// 아이템 제거 (그리드 인스턴스 직접 지정)
        /// </summary>
        public bool TryRemoveItem(ItemInstance instance, int amount = 1)
        {
            // 핫바에서도 해당 슬롯 참조 제거
            for (int i = 0; i < _hotbar.Length; i++)
            {
                if (_hotbar[i] == instance && instance.count - amount <= 0)
                    _hotbar[i] = null;
            }
            return _grid.TryRemove(instance, amount);
        }

        /// <summary>
        /// ItemData 기준으로 1개 제거 (편의용)
        /// </summary>
        public bool TryConsumeOne(Item.ItemBase data)
        {
            foreach (var item in _grid.Items)
            {
                if (item.data == data && item.count > 0)
                    return TryRemoveItem(item, 1);
            }
            return false;
        }

        public bool Has(Item.ItemBase data, int amount = 1) => _grid.Has(data, amount);
        public int CountOf(Item.ItemBase data)              => _grid.CountOf(data);

        /// <summary>itemId(dataId)로 인벤토리 전체 수량 조회 (조합 재료 확인용)</summary>
        public int GetTotalCount(string itemId)
        {
            int total = 0;
            foreach (var inst in _grid.Items)
                if (inst.data.dataId == itemId) total += inst.count;
            return total;
        }

        /// <summary>itemId로 지정 수량만큼 제거. 여러 스택에 걸쳐 제거한다.</summary>
        public void RemoveItems(string itemId, int amount)
        {
            int remaining = amount;
            foreach (var inst in new List<ItemInstance>(_grid.Items))
            {
                if (remaining <= 0) break;
                if (inst.data.dataId != itemId) continue;
                int remove = Mathf.Min(remaining, inst.count);
                TryRemoveItem(inst, remove);
                remaining -= remove;
            }
        }

        /// <summary>ItemBase + 수량으로 추가 (CraftingRepository에서 결과 아이템 지급용)</summary>
        public bool AddItem(Item.ItemBase data, int count = 1) => TryAddItem(data, count);

        // =====================================================================
        // 장착 / 해제
        // =====================================================================

        /// <summary>
        /// instance를 해당 부위에 장착한다.
        /// 이미 다른 아이템이 장착되어 있으면 먼저 해제한다.
        /// 아이템은 그리드에도 유지된 채 장착 슬롯에 추가로 참조된다.
        /// </summary>
        public bool TryEquip(ItemInstance instance)
        {
            if (instance?.data is not Item.EquippableItem eq) return false;

            int idx = (int)eq.slot;
            if (_equips[idx] != null)
                _equips[idx] = null; // 기존 장비 해제 (아이템은 그리드에 잔류)

            _equips[idx] = instance;
            OnEquipmentChanged?.Invoke();
            return true;
        }

        /// <summary>해당 부위 장비를 해제한다. 아이템은 그리드에 그대로 유지.</summary>
        public bool TryUnequip(Item.EquipSlot slot)
        {
            int idx = (int)slot;
            if (_equips[idx] == null) return false;
            _equips[idx] = null;
            OnEquipmentChanged?.Invoke();
            return true;
        }

        /// <summary>모든 부위의 장비 보너스 합산을 반환한다.</summary>
        public void GetEquipBonuses(out float hpBonus, out float speedBonus, out float jumpBonus)
        {
            hpBonus = 0f; speedBonus = 0f; jumpBonus = 0f;
            foreach (var inst in _equips)
            {
                if (inst?.data is Item.EquippableItem eq)
                {
                    hpBonus    += eq.hpBonus;
                    speedBonus += eq.moveSpeedBonus;
                    jumpBonus  += eq.jumpHeightBonus;
                }
            }
        }

        /// <summary>
        /// 스택을 splitAmount만큼 분할해 새 슬롯에 배치한다. (InventoryGrid.TrySplit 래퍼)
        /// </summary>
        public ItemInstance TrySplitItem(ItemInstance source, int splitAmount)
            => _grid.TrySplit(source, splitAmount);

        /// <summary>
        /// 같은 dataId 두 스택을 합친다. from이 소진되면 핫바 참조도 정리한다. (InventoryGrid.TryMerge 래퍼)
        /// </summary>
        public bool TryMergeItem(ItemInstance from, ItemInstance to)
        {
            int merged = _grid.TryMerge(from, to);
            // from이 그리드에서 사라졌으면 핫바 참조 제거
            if (merged > 0 && from.count <= 0)
            {
                for (int i = 0; i < _hotbar.Length; i++)
                    if (_hotbar[i] == from) _hotbar[i] = null;
            }
            return merged > 0;
        }

        // =====================================================================
        // 핫바 관리
        // =====================================================================
        
        /// <summary>
        /// 핫바 슬롯에 그리드 아이템 할당
        /// </summary>
        public void AssignToHotbar(int slot, ItemInstance instance)
        {
            if (slot < 0 || slot >= _hotbar.Length) return;
            _hotbar[slot] = instance;
        }

        public void ClearHotbarSlot(int slot)
        {
            if (slot < 0 || slot >= _hotbar.Length) return;
            _hotbar[slot] = null;
        }

        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= _hotbar.Length) return;
            _selectedHotbarSlot = slot;
        }

        public void SelectNext()
        {
            _selectedHotbarSlot = (_selectedHotbarSlot + 1) % hotbarSize;
        }

        public void SelectPrevious()
        {
            _selectedHotbarSlot = (_selectedHotbarSlot + hotbarSize - 1) % hotbarSize;
        }
    }
}
