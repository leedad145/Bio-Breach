// =============================================================================
// IInputProvider.cs - 마우스 버튼 입력 상태 (ISP: 입력만 담당)
// =============================================================================
namespace BioBreach.Engine.Item
{
    /// <summary>매 프레임 PlayerController가 캐시한 마우스 버튼 입력 상태.</summary>
    public interface IInputProvider
    {
        bool PrimaryDown   { get; }
        bool PrimaryHeld   { get; }
        bool SecondaryDown { get; }
        bool SecondaryHeld { get; }
    }
}
