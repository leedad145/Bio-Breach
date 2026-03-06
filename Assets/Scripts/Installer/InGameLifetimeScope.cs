using UnityEngine;
using VContainer;
using VContainer.Unity;
using BioBreach.Engine.Data;
using BioBreach.Controller.Enemy;
using BioBreach.Controller.Turret;

namespace BioBreach.Installer
{
    public class InGameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // ── Repository 싱글톤 등록 ────────────────────────────────────────
            // GameDataLoader.EnsureLoaded()가 정적 인스턴스를 초기화한다.
            // 같은 인스턴스를 VContainer에 등록 → 정적 접근과 [Inject]가 동일 객체.
            GameDataLoader.EnsureLoaded();

            builder.RegisterInstance(GameDataLoader.Enemies);
            builder.RegisterInstance(GameDataLoader.Turrets);
            builder.RegisterInstance(GameDataLoader.Items);

            // ── 씬에 배치된 MonoBehaviour 자동 주입 ──────────────────────────
            // RegisterComponentInHierarchy : 씬 계층에서 컴포넌트를 찾아 등록하고
            // [Inject] 메서드를 자동으로 호출해 준다.
            builder.RegisterComponentInHierarchy<EnemySpawner>();
            builder.RegisterComponentInHierarchy<TurretController>();
        }
    }
}
