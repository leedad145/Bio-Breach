// =============================================================================
// PlaceableItem.cs - 설치 아이템 (Prefab을 월드에 배치)
// =============================================================================
using UnityEngine;
using BioBreach.Engine.Inventory;

namespace BioBreach.Engine.Item
{
    public class PlaceableItem : ItemBase
    {
        public GameObject placeablePrefab;
        public GameObject previewPrefab;
        public float      placeDistance = 10f;

        public override ActionResult Action1(IPlayerContext ctx)
        {
            if (!ctx.PrimaryDown || !ctx.HasHit || placeablePrefab == null) return ActionResult.None;
            Vector3    pos = ctx.Hit.point + ctx.Hit.normal * ctx.PlaceNormalOffset;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, ctx.Hit.normal);
            if (!ctx.CanPlaceAt(pos)) return ActionResult.None;
            ctx.SpawnObject(placeablePrefab, pos, rot);
            return ActionResult.Consume(1);
        }

        public override ActionResult Action2(IPlayerContext ctx) => ActionResult.None;
    }
}
