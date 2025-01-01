using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityMD4 = UnityEditor.Build.Pipeline.Utilities.MD4;

namespace AssetsOfRain.Editor.VirtualAssets
{
    public class VirtualAddressableAssetGroup : AddressableAssetGroup
    {
        [Serializable]
        public struct SerializableAssetBundleLocation
        {
            public string primaryKey;
            public string internalId;
            public string providerId;
            public AssetBundleRequestOptions data;

            public SerializableAssetBundleLocation(IResourceLocation bundleLocation)
            {
                primaryKey = bundleLocation.PrimaryKey;
                internalId = Path.Combine("{UnityEngine.AddressableAssets.Addressables.RuntimePath}", "StandaloneWindows64", Path.GetFileName(bundleLocation.InternalId));
                providerId = bundleLocation.ProviderId;
                data = (AssetBundleRequestOptions)bundleLocation.Data;
            }
        }

        public override bool ReadOnly => true;

        public string bundleName;
        public SerializableAssetBundleLocation location;
        public SerializableAssetBundleLocation[] dependencies;

        public void Init(IResourceLocation bundleLocation, IList<IResourceLocation> possibleDependencies)
        {
            bundleName = Path.ChangeExtension(((AssetBundleRequestOptions)bundleLocation.Data).BundleName, "bundle");
            location = new SerializableAssetBundleLocation(bundleLocation);

            var assetBundleResource = Addressables.LoadAssetAsync<IAssetBundleResource>(bundleLocation).WaitForCompletion();
            using SerializedObject serializedObject = new SerializedObject(assetBundleResource.GetAssetBundle());
            var m_Dependencies = serializedObject.FindProperty("m_Dependencies");
            HashSet<string> internalDependencyNames = new HashSet<string>();
            for (int i = 0; i < m_Dependencies.arraySize; i++)
            {
                string internalDependencyName = m_Dependencies.GetArrayElementAtIndex(i).stringValue;
                internalDependencyNames.Add(internalDependencyName);
            }
            if (internalDependencyNames.Count == 0)
            {
                dependencies = Array.Empty<SerializableAssetBundleLocation>();
                return;
            }
            List<SerializableAssetBundleLocation> foundDependencies = new List<SerializableAssetBundleLocation>();
            using UnityMD4 hashingAlgorithm = UnityMD4.Create();
            foreach (IResourceLocation possibleDependency in possibleDependencies)
            {
                if (possibleDependency.Data is not AssetBundleRequestOptions assetBundleRequestOptions)
                {
                    continue;
                }

                string dependencyBundleName = Path.ChangeExtension(assetBundleRequestOptions.BundleName, "bundle");
                byte[] dependencyBundleNameHash = hashingAlgorithm.ComputeHash(Encoding.ASCII.GetBytes(dependencyBundleName));
                string internalName = "cab-" + BitConverter.ToString(dependencyBundleNameHash).Replace("-", "").ToLower();
                if (internalDependencyNames.Remove(internalName))
                {
                    foundDependencies.Add(new SerializableAssetBundleLocation(possibleDependency));
                    if (internalDependencyNames.Count == 0)
                    {
                        break;
                    }
                }
            }
            if (internalDependencyNames.Count > 0)
            {
                Debug.LogWarning($"VirtualAddressableAssetGroup {Name} failed to locate the following dependencies: {string.Join(", ", internalDependencyNames)}");
            }
            dependencies = foundDependencies.ToArray();
        }
    }
}
