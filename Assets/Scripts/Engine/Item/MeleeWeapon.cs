// =============================================================================
// MeleeWeapon.cs - 공격 능력 강화 아이템 (스탯 컨테이너)
// 실제 공격 로직은 PlayerController.HandleAction 에서 처리된다.
// 아이템을 손에 들면 맨손 대비 damage/reach/radius 버프를 준다.
// =============================================================================
using UnityEngine;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    public class MeleeWeapon : ItemBase
    {
        public float attackDamage = 25f;
        public float attackReach  = 3f;
        public float attackRadius = 1.5f;

        // 스탯 컨테이너 역할만 하므로 액션은 아무것도 하지 않는다.
        public override ActionResult Action1(IPlayerContext ctx) => ActionResult.None;
        public override ActionResult Action2(IPlayerContext ctx) => ActionResult.None;
    }
}
