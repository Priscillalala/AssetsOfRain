using UnityEditor.AddressableAssets.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace AssetsOfRain.Editor.VirtualAssets
{
    [Serializable]
    public class VirtualAddressableAssetCollection
    {
        private readonly string directory;
        private readonly string groupsDirectory;
        private readonly List<SerializableAssetRequest> assetRequests;

        public VirtualAddressableAssetCollection(string directory, string groupsDirectory)
        {
            this.directory = directory;
            this.groupsDirectory = groupsDirectory;
            assetRequests = new List<SerializableAssetRequest>();
        }

        public List<SerializableAssetRequest> GetAssetRequests() => new List<SerializableAssetRequest>(assetRequests);

        private string GetVirtualAssetPath(SerializableAssetRequest assetRequest)
        {
            string virtualAssetDirectory = Path.Combine(directory, Path.GetDirectoryName(assetRequest.primaryKey));
            string virtualAssetFileName = Path.ChangeExtension(Path.GetFileName(assetRequest.primaryKey), VirtualAddressableAssetImporter.EXTENSION);
            return Path.Combine(virtualAssetDirectory, assetRequest.assemblyQualifiedTypeName, virtualAssetFileName);
        }

        public void ImportVirtualAsset(SerializableAssetRequest assetRequest, out string virtualAssetPath)
        {
            Debug.Log($"ImportVirtualAsset: {assetRequest.primaryKey}");

            if (!assetRequests.Contains(assetRequest))
            {
                assetRequests.Add(assetRequest);
            }

            virtualAssetPath = GetVirtualAssetPath(assetRequest);
            if (!File.Exists(virtualAssetPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(virtualAssetPath));
                File.Create(virtualAssetPath).Close();
                AssetDatabase.ImportAsset(virtualAssetPath);
            }
            if (AssetImporter.GetAtPath(virtualAssetPath) is not VirtualAddressableAssetImporter importer)
            {
                return;
            }
            importer.primaryKey = assetRequest.primaryKey;
            importer.assemblyQualifiedTypeName = assetRequest.assemblyQualifiedTypeName;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            /*AddressableAssetSettings aaSettings = AddressableAssetSettingsDefaultObject.Settings;


            string groupName = fileName.Substring(0, endIndex);
            string groupPath = Path.Combine(testDirectory, Path.ChangeExtension(groupName, "asset")); 
            
            string bundleName = Path.ChangeExtension(assetBundleRequestOptions.BundleName, "bundle");

            ExternalAddressableAssetGroup externalGroup = AssetDatabase.LoadAssetAtPath<ExternalAddressableAssetGroup>(groupPath);
            if (!externalGroup)
            {
                externalGroup = ScriptableObject.CreateInstance<ExternalAddressableAssetGroup>();
                //externalGroup.Initialize(this, validName, GUID.Generate().ToString(), readOnly);
                externalGroup.Name = groupName;
                externalGroup.externalBundleName = bundleName;
                AssetDatabase.CreateAsset(externalGroup, groupPath);
            }*/
        }

        public void DeleteVirtualAsset(SerializableAssetRequest assetRequest)
        {
            assetRequests.Remove(assetRequest);
            string virtualAssetPath = GetVirtualAssetPath(assetRequest);
            AssetDatabase.DeleteAsset(virtualAssetPath);
        }

        public bool TryMoveVirtualAsset(SerializableAssetRequest assetRequest, IEnumerable<IResourceLocation> allowedAssetLocations)
        {
            foreach (var assetLocation in allowedAssetLocations)
            {
                if (Path.GetFileName(assetLocation.PrimaryKey) != Path.GetFileName(assetRequest.primaryKey) || assetLocation.ResourceType != assetRequest.AssetType)
                {
                    continue;
                }
                SerializableAssetRequest newAssetRequest = new SerializableAssetRequest
                {
                    primaryKey = assetLocation.PrimaryKey,
                    AssetType = assetLocation.ResourceType,
                };
                if (assetRequests.Contains(newAssetRequest))
                {
                    continue;
                }
                string oldVirtualAssetPath = GetVirtualAssetPath(assetRequest);
                string newVirtualAssetPath = GetVirtualAssetPath(newAssetRequest);
                AssetDatabase.MoveAsset(oldVirtualAssetPath, newVirtualAssetPath);
                DeleteVirtualAsset(assetRequest);
                ImportVirtualAsset(newAssetRequest, out _);
                return true;
            }
            return false;
        }
    }
}
