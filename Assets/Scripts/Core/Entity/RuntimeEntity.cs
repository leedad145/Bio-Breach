// =============================================================================
// RuntimeEntity.cs - EntityBase의 인스턴스화 가능한 구현체
// Model 레이어 — UnityEngine 의존성 없음 (순수 C#)
// =============================================================================

namespace BioBreach.Core.Entity
{
    /// <summary>
    /// EntityBase를 MonoBehaviour 레이어에서 직접 생성하기 위한 구체 구현체.
    /// EntityMonoBehaviour 내부에서만 인스턴스화되어 사용됨.
    /// </summary>
    public sealed class RuntimeEntity : EntityBase
    {
        public RuntimeEntity(string displayName, float maxHp) : base(displayName, maxHp) { }
    }
}
