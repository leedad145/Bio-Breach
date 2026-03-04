using BioBreach.Engine.Inventory;
using BioBreach.Core.Voxel;
using UnityEngine;

namespace BioBreach.Engine.Item
{
    /// <summary>
    /// PlayerController가 아이템에 노출하는 컨텍스트 인터페이스.
    /// ItemDataSO.BindToPlayer(ItemInstance, IPlayerContext) 에서 람다 캡처에 사용.
    /// PlayerController가 이 인터페이스를 직접 구현하므로 별도 객체 불필요.
    /// </summary>
    public interface IPlayerContext
    {
        // ── 인벤토리 ────────────────────────────────────────────────────────
        PlayerInventory Inventory { get; }

        // ── 배치 ────────────────────────────────────────────────────────────
        float PlaceNormalOffset { get; }
        bool  CanPlaceAt(Vector3 pos);

        // ── Raycast ─────────────────────────────────────────────────────────
        bool       HasHit { get; }
        RaycastHit Hit    { get; }

        // ── 공격 파라미터 ────────────────────────────────────────────────────
        Vector3 AttackOrigin    { get; }
        Vector3 AttackDirection { get; }

        // ── 입력 상태 (매 프레임 PlayerController가 캐시) ──────────────────
        bool PrimaryDown   { get; }
        bool PrimaryHeld   { get; }
        bool SecondaryDown { get; }
        bool SecondaryHeld { get; }

        // ── 지형 조작 ────────────────────────────────────────────────────────
        VoxelType GetVoxelTypeAt(Vector3 worldPos);
        float     ModifyTerrain(Vector3 pos, float radius, float strength, VoxelType type);

        // ── 플레이어 스탯 변경 ───────────────────────────────────────────────
        void AddMoveSpeed(float v);
        void AddJumpHeight(float v);
    }
}
