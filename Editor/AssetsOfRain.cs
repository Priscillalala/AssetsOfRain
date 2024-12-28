using UnityEditor;
using UnityEngine.AddressableAssets;
using AddressableBrowserPlus = AssetsOfRain.Editor.Browser.AddressableBrowser;

namespace AssetsOfRain.Editor
{
    public static class AssetsOfRain
    {
        public const string DATA_DIRECTORY = "Assets/AssetsOfRainData";
        public const string MENU_ROOT = "Tools/Assets of Rain/";

#if TK_ADDRESSABLE
        [MenuItem(MENU_ROOT + "Addressable Browser+")]
        public static void AddressableBrowserPlus()
        {
            EditorWindow.GetWindow<AddressableBrowserPlus>();
        }
#endif

        [MenuItem(MENU_ROOT + "Refresh Addressable Shaders")]
        public static void RefreshAddressableShaders()
        {
            const string MESSAGE = "Assets of Rain will discover all valid addressable shaders and reimport them. Currently invalid shaders will be removed.";
            if (EditorUtility.DisplayDialog("Refresh addressable shaders?", MESSAGE, "Refresh Shaders", "Cancel"))
            {
                AssetsOfRainManager.GetInstance().RefreshVirtualShaders();
            }
        }

        [MenuItem(MENU_ROOT + "Refresh All Addressable Assets")]
        public static void RefreshAllAddressableAssets()
        {
            const string MESSAGE = "Assets of Rain will first refresh addressable shaders, then reimport all previously requested addressable assets.";
            if (EditorUtility.DisplayDialog("Refresh all addressable assets", MESSAGE, "Refresh Assets", "Cancel"))
            {
                AssetsOfRainManager.GetInstance().RefreshVirtualAssets();
            }
        }

        [MenuItem(MENU_ROOT + "Delete All Addressable Assets")]
        public static void RebuildAllAddressableAssets()
        {
            const string MESSAGE = @"Assets of Rain will delete all physical representations of addressable assets within the project. Previously requested assets are recorded and will return on refresh.

This is only necessary if your asset database has been corrupted in some way!";
            
            if (EditorUtility.DisplayDialog("Delete all addressable assets", MESSAGE, "Delete Assets", "Cancel"))
            {
                AssetsOfRainManager.GetInstance().DeleteVirtualAssets();
            }
        }
    }
}
