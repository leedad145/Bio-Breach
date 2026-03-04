// =============================================================================
// ItemData.cs - 아이템 정의 ScriptableObject + 액션 컨텍스트
// =============================================================================
using UnityEngine;
using BioBreach.Core.Voxel;
using BioBreach.Core.Item;
namespace BioBreach.Engine.Item
{
    // =========================================================================
    // ItemDataSO
    // =========================================================================

    /// <summary>
    /// 아이템 정의 데이터 (ScriptableObject)
    /// Create > MarchingCubes > ItemData 로 생성
    /// </summary>
    [CreateAssetMenu(menuName = "MarchingCubes/ItemData", fileName = "NewItem")]
    public class ItemDataSO : ScriptableObject
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
        [Tooltip("그리드에서 차지하는 칸 수 (가로)")]
        [Min(1)] public int gridWidth = 1;

        [Tooltip("그리드에서 차지하는 칸 수 (세로)")]
        [Min(1)] public int gridHeight = 1;

        [Header("카테고리")]
        public ItemCategory category = ItemCategory.Usable;

        // =====================================================================
        // VoxelBlock 전용
        // =====================================================================

        [Header("VoxelBlock 설정 (category = VoxelBlock일 때만 사용)")]
        public VoxelType voxelType = VoxelType.Protein;
        public VoxelEditMode editMode = VoxelEditMode.Both;

        [Tooltip("지형 수정 반경")]
        public float editRadius = 3f;

        [Tooltip("수정 강도")]
        public float editStrength = 0.3f;

        // =====================================================================
        // Placeable 전용
        // =====================================================================

        [Header("Placeable 설정 (category = Placeable일 때만 사용)")]
        [Tooltip("실제로 배치될 프리팹")]
        public GameObject placeablePrefab;

        [Tooltip("미리보기 메시 (null이면 placeablePrefab에서 자동 추출)")]
        public GameObject previewPrefab;

        [Tooltip("배치 가능 최대 거리")]
        public float placeDistance = 10f;

        // =====================================================================
        // Usable 전용
        // =====================================================================

        [Header("Usable 설정 (category = Usable일 때만 사용)")]
        public UsableEffect effect = UsableEffect.Heal;
        public float effectValue = 30f;
        public float effectDuration = 0f; // 0 = 즉발

        // =====================================================================
        // 스택
        // =====================================================================

        [Header("스택")]
        [Tooltip("최대 스택 수 (1이면 스택 불가)")]
        [Min(1)] public int maxStack = 99;

        // =====================================================================
        // 액션 (PlayerController가 컨텍스트를 만들어 위임)
        // =====================================================================

        /// <summary>
        /// 주 행동 (좌클릭). 행동이 실제로 발생하면 true 반환.
        /// 서브클래스에서 override해 커스텀 동작 가능.
        /// </summary>
        public virtual bool OnAction1(ItemActionContext ctx) => category switch
        {
            ItemCategory.VoxelBlock => VoxelDig(ctx),
            ItemCategory.Placeable  => PlaceObject(ctx),
            _                       => false,
        };

        /// <summary>
        /// 보조 행동 (우클릭). 행동이 실제로 발생하면 true 반환.
        /// 서브클래스에서 override해 커스텀 동작 가능.
        /// </summary>
        public virtual bool OnAction2(ItemActionContext ctx) => category switch
        {
            ItemCategory.VoxelBlock => VoxelPlace(ctx),
            ItemCategory.Usable     => UseItem(ctx),
            _                       => false,
        };

        // ── 내부 구현 ────────────────────────────────────────────────────────

        private bool VoxelDig(ItemActionContext ctx)
        {
            if (!ctx.PrimaryHeld || editMode == VoxelEditMode.Add || !ctx.HasHit) return false;
            Vector3 digPoint = ctx.Hit.point - ctx.Hit.normal * 0.1f;
            if (ctx.GetVoxelTypeAt(digPoint) == VoxelType.Air) return false;

            float dugAmount = ctx.ModifyTerrain(digPoint, editRadius, editStrength, VoxelType.Air);
            if (dugAmount <= 0f) return false;

            // 파낸 양 / editStrength = 정규화 비율.
            // 1.0 이상이면 확정 1개, 소수 부분은 확률로 추가 처리.
            int   count      = ProportionalItemCount(dugAmount, editStrength);
            if (count > 0) ctx.Inventory.TryAddItem(this, count);
            return true;
        }

        /// <summary>파낸 밀도(dugAmount)와 강도(strength) 기준으로 아이템 수를 확률적으로 결정.</summary>
        private static int ProportionalItemCount(float dugAmount, float strength)
        {
            float normalized = dugAmount / strength;        // Protein·hardness=1 → ≈1.0
            int   count      = Mathf.FloorToInt(normalized);
            float fraction   = normalized - count;
            if (Random.value < fraction) count++;
            return count;
        }

        private bool VoxelPlace(ItemActionContext ctx)
        {
            if (!ctx.SecondaryHeld || editMode == VoxelEditMode.Remove || !ctx.HasHit) return false;
            if (!ctx.Inventory.Has(this)) return false;
            Vector3 placePoint = ctx.Hit.point + ctx.Hit.normal * 0.1f;
            ctx.ModifyTerrain(placePoint, editRadius * 0.5f, -editStrength, voxelType);
            ctx.Inventory.TryRemoveItem(ctx.Item, 1);
            return true;
        }

        private bool PlaceObject(ItemActionContext ctx)
        {
            if (!ctx.PrimaryDown || !ctx.HasHit || placeablePrefab == null) return false;
            Vector3    pos = ctx.Hit.point + ctx.Hit.normal * ctx.PlaceNormalOffset;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, ctx.Hit.normal);
            if (ctx.CanPlaceAt != null && !ctx.CanPlaceAt(pos)) return false;
            Instantiate(placeablePrefab, pos, rot);
            ctx.Inventory.TryRemoveItem(ctx.Item, 1);
            return true;
        }

        private bool UseItem(ItemActionContext ctx)
        {
            if (!ctx.SecondaryDown) return false;
            switch (effect)
            {
                case UsableEffect.Heal:
                    Debug.Log($"[Usable] 체력 {effectValue} 회복");
                    break;
                case UsableEffect.SpeedBoost:
                    ctx.AddMoveSpeed?.Invoke(effectValue);
                    Debug.Log($"[Usable] 이속 +{effectValue}");
                    break;
                case UsableEffect.JumpBoost:
                    ctx.AddJumpHeight?.Invoke(effectValue);
                    Debug.Log($"[Usable] 점프 +{effectValue}");
                    break;
            }
            ctx.Inventory.TryRemoveItem(ctx.Item, 1);
            return true;
        }
    }
}
