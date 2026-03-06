using UnityEngine;
using VContainer;
using VContainer.Unity;
using BioBreach.Systems;

namespace BioBreach.Installer
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentOnNewGameObject<GameManager>(Lifetime.Singleton)
                .DontDestroyOnLoad();
        }
    }
}
