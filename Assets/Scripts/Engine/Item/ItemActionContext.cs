using BioBreach.Engine.Inventory;
using BioBreach.Core.Voxel;

using UnityEngine;

namespace BioBreach.Engine.Item
{
    // =========================================================================
    // ItemActionContext - PlayerController → 아이템 액션 호출용 컨텍스트
    // =========================================================================

    /// <summary>
    /// PlayerController가 아이템 액션에 필요한 정보를 담아 전달하는 컨텍스트.
    /// Engine 어셈블리 안에서만 정의되므로, Systems(WorldManager) 참조는
    /// 델리게이트로 주입받아 어셈블리 역참조를 방지한다.
    /// </summary>
    public class ItemActionContext
    {
        // ── 인벤토리 ──────────────────────────────────────────────────────────
        public PlayerInventory Inventory;

        // ── 플레이어 파생 값 ──────────────────────────────────────────────────
        /// <summary>설치 위치 법선 오프셋 (PlayerController.placeNormalOffset)</summary>
        public float PlaceNormalOffset;

        // ── WorldManager 조작 델리게이트 (PlayerController가 주입) ────────────
        /// <summary>worldPoint 위치의 VoxelType을 반환</summary>
        public System.Func<Vector3, VoxelType> GetVoxelTypeAt;

        /// <summary>
        /// worldManager.ModifyTerrain(center, radius, strength, type)
        /// 반환값: 파기 시 실제 제거된 고체 밀도 합계(≥0). 설치/변화 없으면 0.
        /// </summary>
        public System.Func<Vector3, float, float, VoxelType, float> ModifyTerrain;

        // ── 플레이어 조작 델리게이트 ──────────────────────────────────────────
        /// <summary>배치 위치가 유효한지 확인 (PlayerController.CanPlaceAt)</summary>
        public System.Func<Vector3, bool> CanPlaceAt;

        /// <summary>이속 증가: moveSpeed += value</summary>
        public System.Action<float> AddMoveSpeed;

        /// <summary>점프력 증가: jumpHeight += value</summary>
        public System.Action<float> AddJumpHeight;

        // ── Raycast 결과 ───────────────────────────────────────────────────────
        public bool       HasHit;
        public RaycastHit Hit;

        // ── 현재 아이템 인스턴스 ──────────────────────────────────────────────
        public ItemInstance Item;

        // ── 공격 방향 (카메라 기준) ────────────────────────────────────────────
        /// <summary>카메라(눈) 월드 위치 — 근접·원거리 무기 공격 출발점</summary>
        public Vector3 AttackOrigin;
        /// <summary>카메라 전방 벡터 — 공격 방향</summary>
        public Vector3 AttackDirection;

        // ── 이번 프레임 입력 상태 (미리 캡처) ────────────────────────────────
        /// <summary>좌클릭 최초 눌림</summary>
        public bool PrimaryDown;
        /// <summary>좌클릭 유지 중</summary>
        public bool PrimaryHeld;
        /// <summary>우클릭 최초 눌림</summary>
        public bool SecondaryDown;
        /// <summary>우클릭 유지 중</summary>
        public bool SecondaryHeld;
    }
}
