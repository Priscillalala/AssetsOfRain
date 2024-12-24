using UnityEditor.AddressableAssets.Settings;

namespace AssetsOfRain.Editor.VirtualAssets
{
    public class VirtualAddressableAssetGroup : AddressableAssetGroup
    {
        public override bool ReadOnly => true;

        public string bundleName;
    }
}
