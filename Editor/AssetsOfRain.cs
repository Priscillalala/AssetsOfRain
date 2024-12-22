using UnityEditor;
using AddressableBrowserPlus = AssetsOfRain.Editor.Browser.AddressableBrowser;

namespace AssetsOfRain.Editor
{
    public static class AssetsOfRain
    {
        public const string DATA_DIRECTORY = "Assets/AssetsOfRainData";
        public const string MENU_ROOT = "Tools/Assets of Rain/";
        public const string NAME = "Assets of Rain";

        [MenuItem(MENU_ROOT + "Addressable Browser+")]
        public static void AddressableBrowserPlus()
        {
            EditorWindow.GetWindow<AddressableBrowserPlus>();
        }

        [MenuItem(MENU_ROOT + "Rebuild All Shaders")]
        public static void RebuildAllShaders()
        {
            if (EditorUtility.DisplayDialog(NAME + ": Rebuild Shaders?", "", "Continue"))
            {
                AssetsOfRainManager.GetInstance().RebuildAddressableShaders();
            }
        }

        [MenuItem(MENU_ROOT + "Rebuild All Assets")]
        public static void RebuildAllAssets()
        {
            if (EditorUtility.DisplayDialog(NAME + ": Rebuild Assets?", "", "Continue"))
            {
                AssetsOfRainManager.GetInstance().RebuildAssets();
            }
        }
    }
}
