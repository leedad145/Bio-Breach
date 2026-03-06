// =============================================================================
// CraftingRecipe.cs - 조합법 데이터 (JSON 바인딩)
// =============================================================================
namespace BioBreach.Engine.Data
{
    /// <summary>조합 재료 하나. item_id + count.</summary>
    [System.Serializable]
    public class CraftingIngredient
    {
        public string itemId;
        public int    count = 1;
    }

    /// <summary>
    /// 조합법 하나.
    /// ingredients: 최대 10개의 재료 (itemId + count)
    /// resultItemId: 완성 아이템 id
    /// resultCount : 완성 아이템 수량 (기본 1)
    /// </summary>
    [System.Serializable]
    public class CraftingRecipe
    {
        public string                id;           // 레시피 고유 id (선택)
        public CraftingIngredient[]  ingredients;  // 최대 10개
        public string                resultItemId;
        public int                   resultCount = 1;
    }
}
