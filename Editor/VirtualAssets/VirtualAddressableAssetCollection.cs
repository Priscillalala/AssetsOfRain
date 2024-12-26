using UnityEditor.AddressableAssets.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using Object = UnityEngine.Object;
using System.Reflection;

namespace AssetsOfRain.Editor.VirtualAssets
{
    [Serializable]
    public class VirtualAddressableAssetCollection
    {
        private static readonly FieldInfo m_Id = typeof(ProfileValueReference).GetField("m_Id", BindingFlags.Instance | BindingFlags.NonPublic);

        [SerializeField]
        private string directory;
        [SerializeField]
        private string groupsDirectory;
        [SerializeField]
        private List<SerializableAssetRequest> assetRequests;

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

        public void ImportVirtualAsset(SerializableAssetRequest assetRequest)
        {
            Debug.Log($"ImportVirtualAsset: {assetRequest.primaryKey}");

            if (!assetRequests.Contains(assetRequest))
            {
                assetRequests.Add(assetRequest);
            }

            string virtualAssetPath = GetVirtualAssetPath(assetRequest);
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
            importer.request = assetRequest;
            //importer.primaryKey = assetRequest.primaryKey;
            //importer.assemblyQualifiedTypeName = assetRequest.assemblyQualifiedTypeName;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            IResourceLocation assetLocation = assetRequest.AssetLocation;
            IResourceLocation bundleLocation = assetLocation.Dependencies.FirstOrDefault(x => x.Data is AssetBundleRequestOptions);
            if (bundleLocation == null)
            {
                return;
            }
            AssetBundleRequestOptions assetBundleRequestOptions = (AssetBundleRequestOptions)bundleLocation.Data;

            AddressableAssetSettings aaSettings = AddressableAssetSettingsDefaultObject.Settings;

            string bundleFileName = Path.GetFileNameWithoutExtension(bundleLocation.InternalId);
            string groupName = bundleFileName[..bundleFileName.LastIndexOf("_assets_")];
            string groupPath = Path.Combine(groupsDirectory, Path.ChangeExtension(groupName, "asset")); 
            
            string bundleName = Path.ChangeExtension(assetBundleRequestOptions.BundleName, "bundle");

            VirtualAddressableAssetGroup group = AssetDatabase.LoadAssetAtPath<VirtualAddressableAssetGroup>(groupPath);
            if (!group)
            {
                group = ScriptableObject.CreateInstance<VirtualAddressableAssetGroup>();
                //group.Initialize(this, validName, GUID.Generate().ToString(), readOnly);
                group.Name = groupName;
                group.bundleName = bundleName;
                BundledAssetGroupSchema bundledAssetGroupSchema = group.AddSchema<BundledAssetGroupSchema>();
                m_Id.SetValue(bundledAssetGroupSchema.LoadPath, "{CatchTheRainbow.CatchTheRainbowPlugin.AddressablesRuntimePath}/StandaloneWindows64");
                bundledAssetGroupSchema.UseAssetBundleCrc = false;
                bundledAssetGroupSchema.UseAssetBundleCrcForCachedBundles = false;
                bundledAssetGroupSchema.IncludeAddressInCatalog = false;
                bundledAssetGroupSchema.IncludeGUIDInCatalog = false;
                bundledAssetGroupSchema.IncludeLabelsInCatalog = false;
                bundledAssetGroupSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
                Directory.CreateDirectory(groupsDirectory);
                AssetDatabase.CreateAsset(group, groupPath);
            }
            aaSettings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(virtualAssetPath), group, true);
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
                    AssetLocation = assetLocation,
                };
                if (assetRequests.Contains(newAssetRequest))
                {
                    continue;
                }
                string oldVirtualAssetPath = GetVirtualAssetPath(assetRequest);
                string newVirtualAssetPath = GetVirtualAssetPath(newAssetRequest);
                AssetDatabase.MoveAsset(oldVirtualAssetPath, newVirtualAssetPath);
                DeleteVirtualAsset(assetRequest);
                ImportVirtualAsset(newAssetRequest);
                return true;
            }
            return false;
        }

        public Object GetVirtualAsset(SerializableAssetRequest assetRequest)
        {
            return AssetDatabase.LoadAssetAtPath(GetVirtualAssetPath(assetRequest), assetRequest.AssetType);
        }

        public bool VirtualAssetExists(SerializableAssetRequest assetRequest)
        {
            return GetVirtualAsset(assetRequest) != null;
        }

        public bool ContainsVirtualAsset(SerializableAssetRequest assetRequest)
        {
            return assetRequests.Contains(assetRequest);
        }
    }
}
