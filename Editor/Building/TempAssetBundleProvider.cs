using System;
using System.ComponentModel;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace AssetsOfRain.Editor.Building
{
    [DisplayName("AssetBundle Dependency Proxy Provider")]
    public class TempAssetBundleProvider : ResourceProviderBase
    {
        public override void Provide(ProvideHandle provideHandle)
        {
            throw new NotImplementedException();
        }
    }
}
