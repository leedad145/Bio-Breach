namespace BioBreach.Core.Item
{
    /// <summary>
    /// 아이템 액션 인터페이스.
    /// PlayerController가 BindToPlayer로 람다를 주입하면,
    /// 이후 Action1() / Action2()를 파라미터 없이 호출할 수 있다.
    /// </summary>
    public interface IItem
    {
        bool Action1();
        bool Action2();
    }
}
