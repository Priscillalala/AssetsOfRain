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
using UnityEngine.ResourceManagement.Util;
using AssetsOfRain.Editor.Building;

namespace AssetsOfRain.Editor.VirtualAssets
{
    [Serializable]
    public class VirtualAddressableAssetCollection
    {
        [SerializeField]
        private string directory;
        [SerializeField]
        private List<SerializableAssetRequest> assetRequests;

        public VirtualAddressableAssetCollection(string directory)
        {
            this.directory = directory;
            assetRequests = new List<SerializableAssetRequest>();
        }

        public List<SerializableAssetRequest> GetAssetRequests() => new List<SerializableAssetRequest>(assetRequests);

        public string GetVirtualAssetPath(SerializableAssetRequest assetRequest)
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
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
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
