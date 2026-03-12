// =============================================================================
// UniversalMiner.cs - 채굴 능력 강화 아이템 (스탯 컨테이너)
// 실제 채굴 로직은 PlayerController.HandleAction 에서 처리된다.
// 아이템을 손에 들면 맨손 대비 radius/strength 버프를 준다.
// =============================================================================
using BioBreach.Core.Voxel;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    public class UniversalMiner : ItemBase
    {
        public float editRadius   = 3f;
        public float editStrength = 0.3f;

        // 채굴 누적값 — PlayerController.HandleAction 에서 업데이트하고
        // HUD(DrawMinerHUD)에서 읽는다.
        private readonly float[] _accumulation = new float[VoxelDatabase.TypeCount];
        public float[] Accumulation => _accumulation;

        // 스탯 컨테이너 역할만 하므로 액션은 아무것도 하지 않는다.
        public override ActionResult Action1(IPlayerContext ctx) => ActionResult.None;
        public override ActionResult Action2(IPlayerContext ctx) => ActionResult.None;
    }
}
