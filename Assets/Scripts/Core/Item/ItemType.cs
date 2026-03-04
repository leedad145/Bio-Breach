// =============================================================================
// ItemType.cs - 아이템 타입 및 카테고리 정의
// =============================================================================

namespace BioBreach.Core.Item
{
    /// <summary>
    /// 아이템 대분류
    /// </summary>
    public enum ItemCategory
    {
        VoxelBlock,     // 지형 수정용 복셀 블록 (파기/설치 시 지형 변형)
        Placeable,      // 설치 아이템 (Prefab을 월드에 배치)
        Usable,         // 사용 아이템 (회복약, 도구 등)
        Weapon,         // 무기 (근접·원거리 공격)
    }

    /// <summary>
    /// 복셀 수정 아이템의 동작 모드
    /// </summary>
    public enum VoxelEditMode
    {
        Add,            // 지형 추가 (설치)
        Remove,         // 지형 제거 (파기)
        Both,           // 파기/설치 모두 가능
    }

    /// <summary>
    /// 사용 아이템 효과 종류
    /// </summary>
    public enum UsableEffect
    {
        None,
        Heal,           // 체력 회복
        SpeedBoost,     // 이속 증가
        JumpBoost,      // 점프력 증가
    }
}
