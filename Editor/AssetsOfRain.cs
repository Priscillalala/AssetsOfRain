using AssetsOfRain.Editor.Util;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThunderKit.Addressable.Config;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using AddressableBrowserPlus = AssetsOfRain.Editor.Browser.AddressableBrowser;

namespace AssetsOfRain.Editor
{
    public static class AssetsOfRain
    {
        public const string DATA_DIRECTORY = "Assets/AssetsOfRainData";
        public const string MENU_ROOT = "Tools/Assets of Rain/";
        public const string PACKAGE_ASSETS_DIRECTORY = "Packages/groovesalad.assetsofrain/Assets";
        public static string AddressablesRuntimePath => "{UnityEngine.AddressableAssets.Addressables.RuntimePath}";

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

        [MenuItem(MENU_ROOT + "Debug/Fix Addressables")]
        public static void FixAddressables()
        {
            var importAddressablesCatalog = ScriptableObject.CreateInstance<ImportAddressableCatalog>();
            importAddressablesCatalog.Execute();
            Object.DestroyImmediate(importAddressablesCatalog);
            AssetDatabase.Refresh();
        }

        [MenuItem(MENU_ROOT + "Run Test")]
        public static void RunTest()
        {
            IResourceLocation materialLocation = Addressables.LoadResourceLocationsAsync("RoR2/Base/AlienHead/matAlienHead.mat", typeof(Material)).WaitForCompletion().FirstOrDefault();
            Addressables.LoadAssetAsync<Material>(materialLocation).WaitForCompletion();
            IResourceLocation bundleLocation = materialLocation.Dependencies.FirstOrDefault(x => x.Data is AssetBundleRequestOptions);
            AssetBundleRequestOptions assetBundleRequestOptions = (AssetBundleRequestOptions)bundleLocation.Data;
            string bundleName = Path.ChangeExtension(assetBundleRequestOptions.BundleName, "bundle");
            //Debug.Log($"Bundle exists? {AssetBundle.GetAllLoadedAssetBundles().Any(x => x.name == bundleName)}");
            //Debug.Log($"Bundle deps: {string.Join(", ", AssetDatabase.GetAssetBundleDependencies(bundleName, true))}");
            AssetBundle loadedBundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(x => x.name == bundleName);

            //Debug.Log($"Bundle deps: {string.Join(", ", EditorUtility.CollectDependencies(new[] { loadedBundle }).Select(x => x.name))}");
            using SerializedObject serializedObject = new SerializedObject(loadedBundle);
            var currentProperty = serializedObject.GetIterator();
            /*while (currentProperty.Next(true))
            {
                if (currentProperty.propertyType == SerializedPropertyType.String)
                {
                    Debug.Log($"{currentProperty.depth} [{currentProperty.name}] Type: {currentProperty.type} Property Type: {currentProperty.propertyType} Value: {currentProperty.stringValue}");

                }
                else
                {
                    Debug.Log($"{currentProperty.depth} [{currentProperty.name}] Type: {currentProperty.type} Property Type: {currentProperty.propertyType}");
                }
            }*/
            var m_Dependencies = serializedObject.FindProperty("m_Dependencies");
            HashSet<string> foundDependencies = new HashSet<string>();
            for (int i = 0; i < m_Dependencies.arraySize; i++)
            {
                string foundDependency = m_Dependencies.GetArrayElementAtIndex(i).stringValue;
                Debug.Log($"Found Bundle Dependency: {foundDependency}");
                foundDependencies.Add(foundDependency);
            }
            Unity5PackedIdentifiers identifier = new Unity5PackedIdentifiers();
            foreach (IResourceLocation dependency in materialLocation.Dependencies)
            {
                if (dependency.Data is AssetBundleRequestOptions options)
                {
                    string internalName = identifier.GenerateInternalFileName(Path.ChangeExtension(options.BundleName, "bundle")).ToLowerInvariant();
                    Debug.Log(internalName);
                    if (foundDependencies.Contains(internalName))
                    {
                        Debug.Log($"{dependency.InternalId} is a dependency");
                    }
                }
            }
        }
    }
}
