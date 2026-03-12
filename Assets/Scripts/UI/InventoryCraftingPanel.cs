// =============================================================================
// InventoryCraftingPanel.cs - 조합 패널 (성체 조합소 범위 내일 때 표시)
// =============================================================================
using UnityEngine;
using BioBreach.Engine.Data;

namespace BioBreach.UI
{
    public class InventoryCraftingPanel
    {
        private Vector2 _scroll;

        public void ResetScroll() => _scroll = Vector2.zero;

        public void Draw(InventoryUIContext ctx)
        {
            int px = ctx.WindowX + ctx.WindowW + InventoryUIContext.CraftGap
                   + InventoryUIContext.EquipW  + InventoryUIContext.CraftGap;
            int py = ctx.WindowY;
            int ph = ctx.WindowH;
            int pw = InventoryUIContext.CraftW;
            Rect win = new Rect(px, py, pw, ph);

            // 배경 + 테두리
            GUI.color = ctx.ColWindowBg;
            GUI.DrawTexture(win, Texture2D.whiteTexture);
            GUI.color = ctx.ColWindowBorder;
            ctx.DrawBorder(win, 1);

            // 타이틀 바
            GUI.color = ctx.ColTitleBar;
            GUI.DrawTexture(new Rect(px, py, pw, InventoryUIContext.TitleH), Texture2D.whiteTexture);
            GUI.color = ctx.ColWindowBorder;
            ctx.DrawBorderBottom(new Rect(px, py, pw, InventoryUIContext.TitleH), 1);
            GUI.color = Color.white;
            GUI.Label(new Rect(px + 10, py + 6, pw - 20, 20), "<b>성체 조합소</b>", ctx.STitle);

            // 스크롤 영역
            var recipes = GameDataLoader.Crafting.All;
            float rowH        = 90f;
            Rect scrollView   = new Rect(px + 4, py + InventoryUIContext.TitleH + 4,
                                         pw - 8, ph - InventoryUIContext.TitleH - 8);
            Rect scrollContent = new Rect(0, 0, scrollView.width - 18, recipes.Count * (rowH + 4));

            _scroll = GUI.BeginScrollView(scrollView, _scroll, scrollContent);

            float iy = 0f;
            foreach (var recipe in recipes)
            {
                bool canCraft = GameDataLoader.Crafting.CanCraft(recipe, ctx.Inventory);
                DrawRecipeRow(ctx, recipe, canCraft, 0, iy, scrollContent.width, rowH);
                iy += rowH + 4f;
            }

            GUI.EndScrollView();
            GUI.color = Color.white;
        }

        void DrawRecipeRow(InventoryUIContext ctx, CraftingRecipe recipe, bool canCraft,
                           float rx, float ry, float rw, float rh)
        {
            Rect row = new Rect(rx, ry, rw, rh);
            GUI.color = canCraft
                ? new Color(0.12f, 0.22f, 0.12f, 0.95f)
                : new Color(0.18f, 0.12f, 0.12f, 0.75f);
            GUI.DrawTexture(row, Texture2D.whiteTexture);
            GUI.color = canCraft ? new Color(0.3f, 0.6f, 0.3f) : new Color(0.35f, 0.2f, 0.2f);
            ctx.DrawBorder(row, 1);

            // 결과 아이템 이름
            string resultName = GameDataLoader.Items.TryGet(recipe.resultItemId, out var rd)
                ? $"{rd.itemName}  ×{recipe.resultCount}"
                : $"{recipe.resultItemId}  ×{recipe.resultCount}";
            GUI.color = canCraft ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(rx + 6, ry + 4, rw - 80, 20), resultName, ctx.STitle);

            // 재료 목록
            float my = ry + 24f;
            if (recipe.ingredients != null)
            {
                foreach (var ing in recipe.ingredients)
                {
                    int  have    = ctx.Inventory.GetTotalCount(ing.itemId);
                    bool ok      = have >= ing.count;
                    string name  = GameDataLoader.Items.TryGet(ing.itemId, out var id2)
                                   ? id2.itemName : ing.itemId;
                    GUI.color = ok ? new Color(0.65f, 1f, 0.65f) : new Color(1f, 0.5f, 0.5f);
                    GUI.Label(new Rect(rx + 8, my, rw - 85, 16),
                              $"• {name}  {have}/{ing.count}", ctx.SLabel);
                    my += 16f;
                }
            }

            // 조합 버튼
            Rect btn = new Rect(rx + rw - 72, ry + rh / 2f - 13, 66, 26);
            if (canCraft)
            {
                GUI.color = Color.white;
                if (GUI.Button(btn, "조합"))
                    GameDataLoader.Crafting.Craft(recipe, ctx.Inventory, GameDataLoader.Items);
            }
            else
            {
                GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
                GUI.Button(btn, "조합");
            }

            GUI.color = Color.white;
        }
    }
}
