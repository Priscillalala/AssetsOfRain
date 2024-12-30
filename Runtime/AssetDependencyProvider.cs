using System.ComponentModel;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace AssetsOfRain
{
    [DisplayName("Dependency on Assets Provider")]
    public class AssetDependencyProvider : ResourceProviderBase
    {
        public override void Provide(ProvideHandle provideHandle)
        {
            provideHandle.Complete<object>(null, true, null);
        }
    }
}
