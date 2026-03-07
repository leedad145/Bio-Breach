// =============================================================================
// ItemRepository.cs - 아이템 JSON 파일 통합 관리 + 팩토리
// =============================================================================
// OCP 준수: 새 아이템 타입은 IItemBuilder 구현체를 추가하기만 하면 됨.
//           ItemRepository 자체를 수정할 필요 없음.
// =============================================================================
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using BioBreach.Engine.Item;
using BioBreach.Engine.Data.Builders;

namespace BioBreach.Engine.Data
{
    public class ItemRepository
    {
        // =====================================================================
        // 빌더 레지스트리 (OCP: 외부에서 RegisterBuilder로 확장 가능)
        // =====================================================================

        private readonly List<IItemBuilder> _builders = new()
        {
            new VoxelBlockBuilder(),
            new UsableItemBuilder(),
            new PlaceableItemBuilder(),
            new MeleeWeaponBuilder(),
            new UniversalMinerBuilder(),
            new RawMaterialBuilder(),
            new EquippableItemBuilder(),
        };

        /// <summary>런타임에 새 빌더를 등록한다. 플러그인 / 모드 확장 용도.</summary>
        public void RegisterBuilder(IItemBuilder builder) => _builders.Add(builder);

        // =====================================================================
        // 데이터 저장소
        // =====================================================================

        private readonly Dictionary<string, ItemData> _dataById = new();

        public IReadOnlyDictionary<string, ItemData> All => _dataById;

        /// <summary>파일 하나를 읽어 통합 딕셔너리에 병합한다. 여러 번 호출 가능.</summary>
        public void LoadFile(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[ItemRepository] Not found: {fullPath}");
                return;
            }
            var entries = JsonConvert.DeserializeObject<List<ItemData>>(File.ReadAllText(fullPath));
            if (entries == null) return;
            foreach (var entry in entries)
                if (!string.IsNullOrEmpty(entry.id))
                    _dataById[entry.id] = entry;
            Debug.Log($"[ItemRepository] Loaded {entries.Count} entries from {Path.GetFileName(fullPath)}");
        }

        public bool TryGet(string id, out ItemData data)
            => _dataById.TryGetValue(id, out data);

        // =====================================================================
        // 팩토리 — IItemBuilder 전략 패턴 사용
        // =====================================================================

        /// <summary>id에 해당하는 ItemBase 서브클래스 인스턴스를 런타임에 생성한다.</summary>
        public ItemBase CreateItem(string id)
        {
            if (!TryGet(id, out var data))
            {
                Debug.LogError($"[ItemRepository] Item '{id}' not found");
                return null;
            }
            return CreateItem(data);
        }

        public ItemBase CreateItem(ItemData data)
        {
            IItemBuilder builder = null;
            foreach (var candidate in _builders)
            {
                if (candidate.CanBuild(data.type)) { builder = candidate; break; }
            }

            if (builder == null)
            {
                Debug.LogError($"[ItemRepository] Unknown item type: '{data.type}' (id={data.id})");
                return null;
            }

            var item = builder.Build(data, this);

            item.dataId      = data.id;
            item.itemName    = data.itemName;
            item.description = data.description;
            item.gridWidth   = Mathf.Max(1, data.gridWidth);
            item.gridHeight  = Mathf.Max(1, data.gridHeight);
            item.maxStack    = Mathf.Max(1, data.maxStack);
            return item;
        }
    }
}
