using UnityEditor;
using UnityEngine.AddressableAssets;
using AddressableBrowserPlus = AssetsOfRain.Editor.Browser.AddressableBrowser;

namespace AssetsOfRain.Editor
{
    public static class AssetsOfRain
    {
        public const string DATA_DIRECTORY = "Assets/AssetsOfRainData";
        public const string MENU_ROOT = "Tools/Assets of Rain/";
        public const string NAME = "Assets of Rain";

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
            if (EditorUtility.DisplayDialog(NAME + ": Refresh Shaders?", "", "Continue"))
            {
                AssetsOfRainManager.GetInstance().RefreshVirtualShaders();
            }
        }

        [MenuItem(MENU_ROOT + "Refresh All Addressable Assets")]
        public static void RefreshAllAddressableAssets()
        {
            if (EditorUtility.DisplayDialog(NAME + ": Refresh Assets?", "", "Continue"))
            {
                AssetsOfRainManager.GetInstance().RefreshVirtualAssets();
            }
        }

        [MenuItem(MENU_ROOT + "Rebuild All Addressable Assets")]
        public static void RebuildAllAddressableAssets()
        {
            if (EditorUtility.DisplayDialog(NAME + ": Rebuild Assets?", "", "Continue"))
            {
                AssetsOfRainManager.GetInstance().RebuildVirtualAssets();
            }
        }
    }
}
