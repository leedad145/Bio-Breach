// =============================================================================
// IItemBuilder.cs - 아이템 생성 전략 인터페이스 (OCP)
// =============================================================================
// 새 아이템 타입 추가 시 이 인터페이스를 구현하는 클래스를 만들고
// ItemRepository에 등록하기만 하면 됨 — ItemRepository 수정 불필요.
// =============================================================================
using BioBreach.Engine.Item;

namespace BioBreach.Engine.Data.Builders
{
    public interface IItemBuilder
    {
        /// <summary>이 빌더가 처리할 수 있는 type 문자열인지 확인한다.</summary>
        bool CanBuild(string type);

        /// <summary>
        /// ItemData로부터 ItemBase 인스턴스를 생성해 반환한다.
        /// 다른 아이템을 참조해야 할 경우 <paramref name="repository"/>를 사용한다.
        /// </summary>
        ItemBase Build(ItemData data, ItemRepository repository);
    }
}
