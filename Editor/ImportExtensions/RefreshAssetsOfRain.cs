using ThunderKit.Common;
using ThunderKit.Core.Config;

[assembly:ImportExtensions]

namespace AssetsOfRain.Editor.ImportExtensions
{
    public class RefreshAssetsOfRain : OptionalExecutor
    {
        public override int Priority => Constants.Priority.AddressableGraphicsImport - 100;
        public override string Name => "Refresh Assets of Rain";
        public override string Description => "Refreshes all addressable assets created by Assets of Rain";

        public override bool Execute()
        {
            if (AssetsOfRainManager.TryGetInstance(out var manager))
            {
                manager.RefreshVirtualAssets();
            }
            return true;
        }
    }
}
