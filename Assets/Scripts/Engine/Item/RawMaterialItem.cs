// =============================================================================
// RawMaterialItem.cs - 기초 광물 아이템
// 조합 재료로만 쓰이며 직접 사용/설치/파기 불가.
// =============================================================================
namespace BioBreach.Engine.Item
{
    public class RawMaterialItem : ItemBase
    {
        public override ActionResult Action1(IPlayerContext ctx) => ActionResult.None;
        public override ActionResult Action2(IPlayerContext ctx) => ActionResult.None;
    }
}
