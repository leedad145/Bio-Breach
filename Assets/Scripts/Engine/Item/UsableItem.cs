// =============================================================================
// UsableItem.cs - 사용 아이템 (회복약, 도구 등)
// =============================================================================
using UnityEngine;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    public enum UsableEffect
    {
        None,
        Heal,
        SpeedBoost,
        JumpBoost,
    }

    public class UsableItem : ItemBase
    {
        public UsableEffect effect         = UsableEffect.Heal;
        public float        effectValue    = 30f;
        public float        effectDuration = 0f;

        public override ActionResult Action1(IPlayerContext ctx) => ActionResult.None;

        public override ActionResult Action2(IPlayerContext ctx)
        {
            switch (effect)
            {
                case UsableEffect.Heal:
                    Debug.Log($"[Usable] 체력 {effectValue} 회복");
                    break;
                case UsableEffect.SpeedBoost:
                    ctx.AddMoveSpeed(effectValue);
                    Debug.Log($"[Usable] 이속 +{effectValue}");
                    break;
                case UsableEffect.JumpBoost:
                    ctx.AddJumpHeight(effectValue);
                    Debug.Log($"[Usable] 점프 +{effectValue}");
                    break;
            }
            return ActionResult.Consume(1);
        }
    }
}
