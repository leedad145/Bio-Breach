// =============================================================================
// CraftingRepository.cs - 조합법 JSON 로드 + 조합 가능 여부 판단
// =============================================================================
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Data
{
    public class CraftingRepository
    {
        readonly List<CraftingRecipe> _recipes = new();

        public IReadOnlyList<CraftingRecipe> All => _recipes;

        public void LoadFile(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[CraftingRepository] Not found: {fullPath}");
                return;
            }
            var list = JsonConvert.DeserializeObject<List<CraftingRecipe>>(File.ReadAllText(fullPath));
            if (list == null) return;
            _recipes.AddRange(list);
            Debug.Log($"[CraftingRepository] Loaded {list.Count} recipes from {Path.GetFileName(fullPath)}");
        }

        /// <summary>
        /// 인벤토리의 현재 아이템으로 만들 수 있는 레시피 목록을 반환한다.
        /// </summary>
        public List<CraftingRecipe> GetCraftable(PlayerInventory inventory)
        {
            var result = new List<CraftingRecipe>();
            foreach (var recipe in _recipes)
            {
                if (CanCraft(recipe, inventory))
                    result.Add(recipe);
            }
            return result;
        }

        /// <summary>레시피의 재료가 인벤토리에 모두 있는지 확인한다.</summary>
        public bool CanCraft(CraftingRecipe recipe, PlayerInventory inventory)
        {
            if (recipe.ingredients == null || recipe.ingredients.Length == 0) return false;
            if (recipe.ingredients.Length > 10) return false; // 최대 10개 제한

            foreach (var ing in recipe.ingredients)
            {
                if (inventory.GetTotalCount(ing.itemId) < ing.count)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 재료를 소모하고 결과 아이템을 인벤토리에 추가한다.
        /// 성공하면 true 반환.
        /// </summary>
        public bool Craft(CraftingRecipe recipe, PlayerInventory inventory, ItemRepository items)
        {
            if (!CanCraft(recipe, inventory)) return false;

            // 결과 아이템 생성 확인
            var resultItem = items.CreateItem(recipe.resultItemId);
            if (resultItem == null)
            {
                Debug.LogError($"[CraftingRepository] 결과 아이템 '{recipe.resultItemId}'을 생성할 수 없습니다.");
                return false;
            }

            // 재료 소모
            foreach (var ing in recipe.ingredients)
                inventory.RemoveItems(ing.itemId, ing.count);

            // 결과 추가
            inventory.AddItem(resultItem, recipe.resultCount);
            Debug.Log($"[CraftingRepository] 조합 완료: {recipe.resultItemId} x{recipe.resultCount}");
            return true;
        }
    }
}
