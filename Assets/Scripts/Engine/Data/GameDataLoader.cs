// =============================================================================
// GameDataLoader.cs - 전체 데이터 로드 진입점
// Repository들을 초기화하고 StreamingAssets/Data/*.json 을 로드한다.
// =============================================================================
using System.IO;
using UnityEngine;
using BioBreach.Engine.Item;

namespace BioBreach.Engine.Data
{
    public static class GameDataLoader
    {
        // =====================================================================
        // 공개 Repository 프로퍼티
        // =====================================================================

        public static EnemyRepository    Enemies  { get; } = new();
        public static TurretRepository  Turrets  { get; } = new();
        public static ItemRepository    Items    { get; } = new();
        public static CraftingRepository Crafting { get; } = new();

        static bool _loaded;

        // =====================================================================
        // 로드
        // =====================================================================

        /// <summary>
        /// 아직 로드되지 않았으면 StreamingAssets/Data/*.json 전부 로드한다.
        /// MonoBehaviour Start/Awake에서 호출할 것.
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_loaded) return;

            string root       = Path.Combine(Application.streamingAssetsPath, "Data");
            string enemyDir   = Path.Combine(root, "Enemies");
            string itemDir    = Path.Combine(root, "Items");
            string worldDir   = Path.Combine(root, "World");

            Enemies.LoadFile(Path.Combine(enemyDir, "enemies.json"));
            Turrets.LoadFile(Path.Combine(itemDir,  "turrets.json"));

            // 아이템은 타입별 파일을 분리해 같은 ItemRepository에 통합
            Items.LoadFile(Path.Combine(itemDir, "voxel_blocks.json"));
            Items.LoadFile(Path.Combine(itemDir, "usables.json"));
            Items.LoadFile(Path.Combine(itemDir, "placeables.json"));
            Items.LoadFile(Path.Combine(itemDir, "melee_weapons.json"));
            Items.LoadFile(Path.Combine(itemDir, "universal_miners.json"));
            Items.LoadFile(Path.Combine(itemDir, "raw_materials.json"));
            Items.LoadFile(Path.Combine(itemDir, "equippables.json"));

            Crafting.LoadFile(Path.Combine(worldDir, "recipes.json"));

            _loaded = true;
        }

        // =====================================================================
        // SO 팩토리 (하위 호환 래퍼 — ItemRepository.CreateSO 에 위임)
        // =====================================================================

        public static Item.ItemBase CreateItemSO(string id) => Items.CreateItem(id);
        public static Item.ItemBase CreateItemSO(ItemData d) => Items.CreateItem(d);
    }
}
