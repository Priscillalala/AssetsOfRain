using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace AssetsOfRain.Editor.VirtualAssets
{
    public class VirtualAddressableAssetGroup : AddressableAssetGroup
    {
        [Serializable]
        public struct AssetBundleDependency
        {
            public string primaryKey;
            public string internalId;
            public AssetBundleRequestOptions data;
        }

        public override bool ReadOnly => true;

        public string bundleName;
        public AssetBundleRequestOptions data;
        public AssetBundleDependency[] dependencies;

        public void Init(IResourceLocation bundleLocation, IList<IResourceLocation> possibleDependencies)
        {
            data = (AssetBundleRequestOptions)bundleLocation.Data;
            bundleName = Path.ChangeExtension(data.BundleName, "bundle");

            var assetBundleResource = Addressables.LoadAssetAsync<IAssetBundleResource>(bundleLocation).WaitForCompletion();
            using SerializedObject serializedObject = new SerializedObject(assetBundleResource.GetAssetBundle());
            var m_Dependencies = serializedObject.FindProperty("m_Dependencies");
            HashSet<string> internalDependencyNames = new HashSet<string>();
            for (int i = 0; i < m_Dependencies.arraySize; i++)
            {
                string internalDependencyName = m_Dependencies.GetArrayElementAtIndex(i).stringValue;
                internalDependencyNames.Add(internalDependencyName);
            }
            List<AssetBundleDependency> foundDependencies = new List<AssetBundleDependency>();
            foreach (IResourceLocation possibleDependency in possibleDependencies)
            {
                if (possibleDependency.Data is not AssetBundleRequestOptions assetBundleRequestOptions)
                {
                    continue;
                }
                string internalName = "cab-" + HashingMethods.Calculate<UnityEditor.Build.Pipeline.Utilities.MD4>(Path.ChangeExtension(assetBundleRequestOptions.BundleName, "bundle"));
                if (internalDependencyNames.Contains(internalName))
                {
                    foundDependencies.Add(new AssetBundleDependency
                    {
                        primaryKey = possibleDependency.PrimaryKey,
                        internalId = Path.Combine("{UnityEngine.AddressableAssets.Addressables.RuntimePath}", "StandaloneWindows64", Path.GetFileName(possibleDependency.InternalId)),
                        data = assetBundleRequestOptions,
                    });
                }
            }
            dependencies = foundDependencies.ToArray();
        }
    }
}
