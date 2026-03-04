// =============================================================================
// ItemDataSO.cs - 아이템 정의 ScriptableObject
// =============================================================================
using UnityEngine;
using BioBreach.Core.Voxel;
using BioBreach.Core.Item;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    [CreateAssetMenu(menuName = "MarchingCubes/ItemData", fileName = "NewItem")]
    public class ItemDataSO : ScriptableObject, IItem
    {
        // =====================================================================
        // 공통
        // =====================================================================

        [Header("기본 정보")]
        public string itemName = "새 아이템";

        [TextArea(2, 4)]
        public string description = "";

        public Sprite icon;

        [Header("인벤토리 그리드 크기")]
        [Min(1)] public int gridWidth  = 1;
        [Min(1)] public int gridHeight = 1;

        [Header("카테고리")]
        public ItemCategory category = ItemCategory.Usable;

        // =====================================================================
        // VoxelBlock 전용
        // =====================================================================

        [Header("VoxelBlock 설정 (category = VoxelBlock일 때만 사용)")]
        public VoxelType voxelType   = VoxelType.Protein;
        public VoxelEditMode editMode = VoxelEditMode.Both;
        public float editRadius      = 3f;
        public float editStrength    = 0.3f;

        // =====================================================================
        // Placeable 전용
        // =====================================================================

        [Header("Placeable 설정 (category = Placeable일 때만 사용)")]
        public GameObject placeablePrefab;
        public GameObject previewPrefab;
        public float placeDistance = 10f;

        // =====================================================================
        // Usable 전용
        // =====================================================================

        [Header("Usable 설정 (category = Usable일 때만 사용)")]
        public UsableEffect effect  = UsableEffect.Heal;
        public float effectValue    = 30f;
        public float effectDuration = 0f;

        // =====================================================================
        // 스택
        // =====================================================================

        [Header("스택")]
        [Min(1)] public int maxStack = 99;

        // =====================================================================
        // 주입 진입점 — PlayerController가 아이템 선택 시 호출
        // =====================================================================

        /// <summary>
        /// PlayerController(IPlayerContext 구현체)가 아이템을 선택했을 때 호출.
        /// instance.SetActions()로 Action1 / Action2 람다를 주입한다.
        /// 서브클래스에서 override해 커스텀 액션 바인딩 가능.
        /// </summary>
        public virtual void BindToPlayer(ItemInstance instance, IPlayerContext ctx)
        {
            instance.SetActions(
                a1: () => OnAction1(ctx, instance),
                a2: () => OnAction2(ctx, instance)
            );
        }

        // IItem 명시적 구현 — ScriptableObject 자체는 직접 호출되지 않음
        bool IItem.Action1() => false;
        bool IItem.Action2() => false;

        // =====================================================================
        // 카테고리별 기본 액션
        // =====================================================================

        protected virtual bool OnAction1(IPlayerContext ctx, ItemInstance instance) => category switch
        {
            ItemCategory.VoxelBlock => VoxelDig(ctx),
            ItemCategory.Placeable  => PlaceObject(ctx, instance),
            _                       => false,
        };

        protected virtual bool OnAction2(IPlayerContext ctx, ItemInstance instance) => category switch
        {
            ItemCategory.VoxelBlock => VoxelPlace(ctx, instance),
            ItemCategory.Usable     => UseItem(ctx, instance),
            _                       => false,
        };

        // ── 내부 구현 ────────────────────────────────────────────────────────

        private bool VoxelDig(IPlayerContext ctx)
        {
            if (!ctx.PrimaryHeld || editMode == VoxelEditMode.Add || !ctx.HasHit) return false;
            Vector3 digPoint = ctx.Hit.point - ctx.Hit.normal * 0.1f;
            if (ctx.GetVoxelTypeAt(digPoint) == VoxelType.Air) return false;

            float dugAmount = ctx.ModifyTerrain(digPoint, editRadius, editStrength, VoxelType.Air);
            if (dugAmount <= 0f) return false;

            int count = ProportionalItemCount(dugAmount, editStrength);
            if (count > 0) ctx.Inventory.TryAddItem(this, count);
            return true;
        }

        private static int ProportionalItemCount(float dugAmount, float strength)
        {
            float normalized = dugAmount / strength;
            int   count      = Mathf.FloorToInt(normalized);
            if (Random.value < normalized - count) count++;
            return count;
        }

        private bool VoxelPlace(IPlayerContext ctx, ItemInstance instance)
        {
            if (!ctx.SecondaryHeld || editMode == VoxelEditMode.Remove || !ctx.HasHit) return false;
            if (!ctx.Inventory.Has(this)) return false;
            Vector3 placePoint = ctx.Hit.point + ctx.Hit.normal * 0.1f;
            ctx.ModifyTerrain(placePoint, editRadius * 0.5f, -editStrength, voxelType);
            ctx.Inventory.TryRemoveItem(instance, 1);
            return true;
        }

        private bool PlaceObject(IPlayerContext ctx, ItemInstance instance)
        {
            if (!ctx.PrimaryDown || !ctx.HasHit || placeablePrefab == null) return false;
            Vector3    pos = ctx.Hit.point + ctx.Hit.normal * ctx.PlaceNormalOffset;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, ctx.Hit.normal);
            if (!ctx.CanPlaceAt(pos)) return false;
            Instantiate(placeablePrefab, pos, rot);
            ctx.Inventory.TryRemoveItem(instance, 1);
            return true;
        }

        private bool UseItem(IPlayerContext ctx, ItemInstance instance)
        {
            if (!ctx.SecondaryDown) return false;
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
            ctx.Inventory.TryRemoveItem(instance, 1);
            return true;
        }
    }
}
