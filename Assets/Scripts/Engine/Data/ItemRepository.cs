// =============================================================================
// ItemRepository.cs - 여러 아이템 JSON 파일을 하나의 딕셔너리로 통합 관리
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using BioBreach.Core.Voxel;
using BioBreach.Engine.Item;

namespace BioBreach.Engine.Data
{
    public class ItemRepository
    {
        readonly Dictionary<string, ItemData> _data = new();

        public IReadOnlyDictionary<string, ItemData> All => _data;

        /// <summary>파일 하나를 읽어 통합 딕셔너리에 병합한다. 여러 번 호출 가능.</summary>
        public void LoadFile(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[ItemRepository] Not found: {fullPath}");
                return;
            }
            var list = JsonConvert.DeserializeObject<List<ItemData>>(File.ReadAllText(fullPath));
            if (list == null) return;
            foreach (var d in list)
                if (!string.IsNullOrEmpty(d.id))
                    _data[d.id] = d;
            Debug.Log($"[ItemRepository] Loaded {list.Count} entries from {Path.GetFileName(fullPath)}");
        }

        public bool TryGet(string id, out ItemData data)
            => _data.TryGetValue(id, out data);

        // =====================================================================
        // SO 팩토리
        // =====================================================================

        /// <summary>id에 해당하는 ItemDataSO 서브클래스 인스턴스를 런타임에 생성한다.</summary>
        public ItemBase CreateItem(string id)
        {
            if (!TryGet(id, out var d))
            {
                Debug.LogError($"[ItemRepository] Item '{id}' not found");
                return null;
            }
            return CreateItem(d);
        }

        public ItemBase CreateItem(ItemData d)
        {
            ItemBase so = d.type switch
            {
                "VoxelBlock"     => BuildVoxelBlock(d),
                "Usable"         => BuildUsable(d),
                "Placeable"      => BuildPlaceable(d),
                "MeleeWeapon"    => BuildMeleeWeapon(d),
                "UniversalMiner" => BuildUniversalMiner(d),
                "RawMaterial"    => new Item.RawMaterialItem(),
                _                => null
            };

            if (so == null)
            {
                Debug.LogError($"[ItemRepository] Unknown item type: '{d.type}' (id={d.id})");
                return null;
            }

            so.dataId      = d.id;
            so.itemName    = d.itemName;
            so.description = d.description;
            so.gridWidth   = Mathf.Max(1, d.gridWidth);
            so.gridHeight  = Mathf.Max(1, d.gridHeight);
            so.maxStack    = Mathf.Max(1, d.maxStack);
            return so;
        }

        static VoxelBlockItem BuildVoxelBlock(ItemData d)
        {
            var so = new VoxelBlockItem();
            so.voxelType    = ParseEnum<VoxelType>(d.voxelType,    VoxelType.Protein);
            so.editMode     = ParseEnum<VoxelEditMode>(d.editMode,  VoxelEditMode.Both);
            so.editRadius   = d.editRadius;
            so.editStrength = d.editStrength;
            return so;
        }

        static UsableItem BuildUsable(ItemData d)
        {
            var so = new UsableItem();
            so.effect         = ParseEnum<UsableEffect>(d.effect, UsableEffect.None);
            so.effectValue    = d.effectValue;
            so.effectDuration = d.effectDuration;
            return so;
        }

        static PlaceableItem BuildPlaceable(ItemData d)
        {
            var so = new PlaceableItem();
            so.placeDistance = d.placeDistance;
            return so;
        }

        static MeleeWeapon BuildMeleeWeapon(ItemData d)
        {
            var so = new MeleeWeapon();
            so.attackDamage = d.meleeAttackDamage;
            so.attackReach  = d.meleeAttackReach;
            so.attackRadius = d.meleeAttackRadius;
            return so;
        }
        UniversalMiner BuildUniversalMiner(ItemData d)
        {
            var so = new UniversalMiner();
            so.editRadius   = d.editRadius;
            so.editStrength = d.editStrength;

            if (d.voxelDropIds != null && d.voxelDropIds.Length > 0)
            {
                so.voxelDrops = new Item.ItemBase[d.voxelDropIds.Length];
                for (int i = 0; i < d.voxelDropIds.Length; i++)
                {
                    var dropId = d.voxelDropIds[i];
                    if (!string.IsNullOrEmpty(dropId))
                        so.voxelDrops[i] = CreateItem(dropId);
                }
            }
            return so;
        }
        static T ParseEnum<T>(string value, T fallback) where T : struct, Enum
        {
            if (Enum.TryParse<T>(value, ignoreCase: true, out var result)) return result;
            Debug.LogWarning($"[ItemRepository] Cannot parse '{value}' as {typeof(T).Name}, using {fallback}");
            return fallback;
        }
    }
}
