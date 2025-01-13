using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

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

            Hash128 typeHash = Hash128.Compute(assetRequest.assemblyQualifiedTypeName);
            typeHash.Append(Path.GetExtension(assetRequest.primaryKey));

            string virtualAssetFileName = Path.ChangeExtension(Path.GetFileName(assetRequest.primaryKey), VirtualAddressableAssetImporter.EXTENSION);
            
            return Path.Combine(virtualAssetDirectory, typeHash.ToString(), virtualAssetFileName);
        }

        public void ImportVirtualAsset(SerializableAssetRequest assetRequest)
        {
            if (!assetRequests.Contains(assetRequest))
            {
                assetRequests.Add(assetRequest);
            }

            string virtualAssetPath = GetVirtualAssetPath(assetRequest);
            string longVirtualAssetPath = "\\\\?\\" + Path.GetFullPath(virtualAssetPath);
            if (!File.Exists(longVirtualAssetPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(longVirtualAssetPath));
                File.Create(longVirtualAssetPath).Close();
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

        public void DeleteVirtualAsset(SerializableAssetRequest assetRequest, bool forgetRequest = true)
        {
            if (forgetRequest)
            {
                assetRequests.Remove(assetRequest);
            }
            string virtualAssetPath = GetVirtualAssetPath(assetRequest);
            AssetDatabase.DeleteAsset(virtualAssetPath);
        }

        // Attempts to move an asset whos primary key might have changed between game builds
        public bool TryMoveVirtualAsset(SerializableAssetRequest assetRequest, IEnumerable<IResourceLocation> allowedAssetLocations, out SerializableAssetRequest newAssetRequest)
        {
            foreach (var assetLocation in allowedAssetLocations)
            {
                if (Path.GetFileName(assetLocation.PrimaryKey) != Path.GetFileName(assetRequest.primaryKey) || assetLocation.ResourceType != assetRequest.AssetType)
                {
                    continue;
                }
                newAssetRequest = new SerializableAssetRequest
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
            newAssetRequest = default;
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
