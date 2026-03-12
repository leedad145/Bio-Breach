// =============================================================================
// InventoryStatsPanel.cs - HP 바 + 스탯 분해 패널 (인벤토리 창 하단)
// =============================================================================
using UnityEngine;
using BioBreach.Systems;
using BioBreach.Controller.Player;

namespace BioBreach.UI
{
    public class InventoryStatsPanel
    {
        public void Draw(InventoryUIContext ctx)
        {
            var ctrl = ctx.Controller;
            if (ctrl == null) return;

            float panelY = ctx.WindowY + InventoryUIContext.TitleH + InventoryUIContext.Padding
                         + ctx.Inventory.gridRows * ctx.Step + InventoryUIContext.Padding;
            float panelX = ctx.WindowX + InventoryUIContext.Padding;
            float panelW = ctx.WindowW - InventoryUIContext.Padding * 2;

            // 구분선
            GUI.color = ctx.ColWindowBorder;
            GUI.DrawTexture(new Rect(ctx.WindowX, panelY - 2f, ctx.WindowW, 1f), Texture2D.whiteTexture);

            // ── HP 바 ──
            float curHp = ctrl.CurrentHp;
            float maxHp = ctrl.MaxHp;
            float ratio = maxHp > 0f ? Mathf.Clamp01(curHp / maxHp) : 0f;

            GUI.color = new Color(0.70f, 0.70f, 0.75f);
            GUI.Label(new Rect(panelX, panelY + 4f, 26f, 16f), "HP", ctx.SLabel);

            Rect barBg = new Rect(panelX + 26f, panelY + 6f, panelW - 26f, 14f);
            GUI.color = new Color(0.14f, 0.07f, 0.07f, 0.95f);
            GUI.DrawTexture(barBg, Texture2D.whiteTexture);

            if (ratio > 0f)
            {
                GUI.color = Color.Lerp(new Color(0.85f, 0.15f, 0.15f), new Color(0.15f, 0.78f, 0.32f), ratio);
                GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * ratio, barBg.height),
                                Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(barBg.x + 4f, barBg.y, barBg.width - 8f, barBg.height),
                      $"{curHp:F0} / {maxHp:F0}", ctx.SLabel);

            // ── 이동 / 점프 스탯 ──
            ctx.Inventory.GetEquipBonuses(out _, out float equipSpeed, out float equipJump);
            float buffSpeed  = ctrl.BuffSpeed;
            float buffJump   = ctrl.BuffJump;
            float skillSpeed = ctrl.SkillSpeedBonus;
            float skillJump  = ctrl.SkillJumpBonus;
            float baseSpeed  = ctrl.BaseMoveSpeed;
            float baseJump   = ctrl.BaseJumpHeight;

            GUI.color = new Color(0.65f, 0.72f, 0.82f);
            GUI.Label(new Rect(panelX, panelY + 26f, panelW, 16f),
                $"이동  {baseSpeed:F1}(기본) +{equipSpeed:F1}(장착) +{buffSpeed:F1}(버프) +{skillSpeed:F1}(스킬) = {ctrl.moveSpeed:F1}",
                ctx.SLabel);
            GUI.Label(new Rect(panelX, panelY + 42f, panelW, 16f),
                $"점프  {baseJump:F1}(기본) +{equipJump:F1}(장착) +{buffJump:F1}(버프) +{skillJump:F1}(스킬) = {ctrl.jumpHeight:F1}",
                ctx.SLabel);

            // ── 감도 / 사거리 ──
            float row4Y = panelY + 58f;
            var movement = ctrl.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                GUI.color = new Color(0.65f, 0.72f, 0.82f);
                GUI.Label(new Rect(panelX,                  row4Y, panelW * 0.5f, 16f),
                          $"감도  {movement.mouseSensitivity:F1}", ctx.SLabel);
            }
            GUI.Label(new Rect(panelX + panelW * 0.5f, row4Y, panelW * 0.5f, 16f),
                      $"사거리  {ctrl.interactDistance:F0}", ctx.SLabel);

            // ── 스킬 포인트 ──
            var skillData = PlayerSkillData.Instance;
            if (skillData != null)
            {
                GUI.color = new Color(0.9f, 0.8f, 0.3f);
                GUI.Label(new Rect(panelX, panelY + 74f, panelW, 16f),
                    $"스킬 포인트  {skillData.SkillPoints}pt  (F키: 스킬 트리)", ctx.SLabel);
            }

            GUI.color = Color.white;
        }
    }
}
