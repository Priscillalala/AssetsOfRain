using AssetsOfRain.Editor.VirtualAssets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Object = UnityEngine.Object;

namespace AssetsOfRain.Editor
{
    public class AssetsOfRainManager : ScriptableObject
    {
        [Serializable]
        public struct AssetRequestInfo
        {
            public string primaryKey;
            public string assemblyQualifiedTypeName;

            public AssetRequestInfo(IResourceLocation assetLocation)
            {
                primaryKey = assetLocation.PrimaryKey;
                assemblyQualifiedTypeName = assetLocation.ResourceType.AssemblyQualifiedName;
            }
        }

        [Serializable]
        public struct AddressableShaderInfo
        {
            public string primaryKey;
            public long identifier;
            public Shader asset;
            public List<Material> materialsWithShader;
        }

        const string MANAGER_FILE_PATH = AssetsOfRain.DATA_DIRECTORY + "/AssetsOfRainManager.asset";
        const string VIRTUAL_ASSETS_DIRECTORY = AssetsOfRain.DATA_DIRECTORY + "/VirtualAssets";
        const string GROUPS_DIRECTORY = AssetsOfRain.DATA_DIRECTORY + "/Groups";

        public List<AssetRequestInfo> assetRequests = new List<AssetRequestInfo>();
        public List<AddressableShaderInfo> addressableShaders = new List<AddressableShaderInfo>();
        public string[] ignoredShaderDirectories = new[]
        {
            "Hopoo Games/Optimized/Switch/",
        };

        private static AssetsOfRainManager instance;

        public static bool TryGetInstance(out AssetsOfRainManager manager)
        {
            manager = instance;
            if (!manager)
            {
                manager = AssetDatabase.LoadAssetAtPath<AssetsOfRainManager>(MANAGER_FILE_PATH);
            }
            return manager != null;
        }

        public static AssetsOfRainManager GetInstance()
        {
            if (TryGetInstance(out AssetsOfRainManager manager))
            {
                return manager;
            }
            instance = CreateInstance<AssetsOfRainManager>();
            instance.name = Path.GetFileNameWithoutExtension(MANAGER_FILE_PATH);
            Directory.CreateDirectory(AssetsOfRain.DATA_DIRECTORY);
            AssetDatabase.CreateAsset(instance, MANAGER_FILE_PATH);
            return instance;
        }

        public static string GetVirtualAssetPath(string primaryKey, string assemblyQualifiedTypeName)
        {
            string virtualAssetDirectory = Path.Combine(VIRTUAL_ASSETS_DIRECTORY, Path.GetDirectoryName(primaryKey));
            string virtualAssetFileName = Path.ChangeExtension(Path.GetFileName(primaryKey), VirtualAddressableAssetImporter.EXTENSION);
            return Path.Combine(virtualAssetDirectory, assemblyQualifiedTypeName, virtualAssetFileName);
        }

        public void RequestAsset(AssetRequestInfo assetRequest)
        {
            Debug.Log($"RequestAsset: {assetRequest.primaryKey}");

            if (!assetRequests.Contains(assetRequest))
            {
                assetRequests.Add(assetRequest);
                EditorUtility.SetDirty(this);
            }

            ImportVirtualAsset(assetRequest.primaryKey, assetRequest.assemblyQualifiedTypeName);
        }

        public void RemoveAsset(AssetRequestInfo assetRequest)
        {
            if (assetRequests.Remove(assetRequest))
            {
                EditorUtility.SetDirty(this);
            }
            DeleteVirtualAsset(assetRequest.primaryKey, assetRequest.assemblyQualifiedTypeName);
        }

        private void ImportVirtualAsset(string primaryKey, string assemblyQualifiedTypeName, out string virtualAssetPath)
        {
            virtualAssetPath = GetVirtualAssetPath(primaryKey, assemblyQualifiedTypeName);
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
            importer.primaryKey = primaryKey;
            importer.assemblyQualifiedTypeName = assemblyQualifiedTypeName;
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

        /*private void MoveVirtualAsset(ref string primaryKey, string newPrimaryKey, string assemblyQualifiedTypeName)
        {
            string oldVirtualAssetPath = GetVirtualAssetPath(primaryKey, assemblyQualifiedTypeName);
            string newVirtualAssetPath = GetVirtualAssetPath(newPrimaryKey, assemblyQualifiedTypeName);
            AssetDatabase.MoveAsset(oldVirtualAssetPath, newVirtualAssetPath);
            primaryKey = newPrimaryKey;
            ImportVirtualAsset(primaryKey, assemblyQualifiedTypeName, newVirtualAssetPath);
        }*/

        private void DeleteVirtualAsset(string primaryKey, string assemblyQualifiedTypeName)
        {
            string virtualAssetPath = GetVirtualAssetPath(primaryKey, assemblyQualifiedTypeName);
            AssetDatabase.DeleteAsset(virtualAssetPath);
        }

        /*string primaryKey = shaderLocation.PrimaryKey;
                    if (!foundShaderKeys.Add(primaryKey))
                    {
                        continue;
                    }
                    Shader shader = Addressables.LoadAssetAsync<Shader>(shaderLocation).WaitForCompletion();
                    if (ignoredShaderDirectories.Any(x => shader.name.StartsWith(x)))
                    {
                        continue;
                    }

                    if (primaryKey != "RoR2/Base/Shaders/HGStandard.shader" && primaryKey != "RoR2/Base/Shaders/HGCloudRemap.shader")
                    {
                        continue;
                    }
                    Debug.Log($"At: {primaryKey}: {shader.name}");

                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader, out _, out long localId);

                    int oldAddressableShaderIndex = oldAddressableShaders.FindIndex(x => x.primaryKey == primaryKey);
                    if (oldAddressableShaderIndex < 0)
                    {
                        oldAddressableShaderIndex = oldAddressableShaders.FindIndex(x => Path.GetFileName(x.primaryKey) == Path.GetFileName(primaryKey));
                    }
                    AddressableShaderInfo addressableShaderInfo;
                    if (oldAddressableShaderIndex < 0)
                    {
                        addressableShaderInfo = default;
                    }
                    else
                    {

                    }



                    ImportVirtualAsset(primaryKey, typeof(Shader).AssemblyQualifiedName);
                    shader = AssetDatabase.LoadAssetAtPath<Shader>(virtualAssetPath);
                    if (!shader)
                    {
                        continue;
                    }
                    AddressableShaderInfo addressableShaderInfo = oldAddressableShaders.FirstOrDefault(x => x.primaryKey == primaryKey);
                    addressableShaderInfo.primaryKey = shaderLocation.PrimaryKey;
                    addressableShaderInfo.identifier = localId;
                    addressableShaderInfo.asset = shader;
                    addressableShaderInfo.materialsWithShader ??= new List<Material>();
                    addressableShaders.Add(addressableShaderInfo);*/

        public AsyncOperationHandle RefreshAddressableShaders()
        {
            Debug.Log("previousAddressableShaders");

            HashSet<string> foundShaderKeys = new HashSet<string>();
            List<IResourceLocation> uniqueShaderLocations = new List<IResourceLocation>();
            foreach (IResourceLocator resourceLocator in Addressables.ResourceLocators)
            {
                foreach (var key in resourceLocator.Keys)
                {
                    if (!resourceLocator.Locate(key, typeof(Shader), out IList<IResourceLocation> locations))
                    {
                        continue;
                    }
                    var shaderLocation = locations.FirstOrDefault();
                    if (shaderLocation != null && foundShaderKeys.Add(shaderLocation.PrimaryKey))
                    {
                        if (shaderLocation.PrimaryKey != "RoR2/Base/Shaders/HGStandard.shader" && shaderLocation.PrimaryKey != "RoR2/Base/Shaders/HGCloudRemap.shader")
                        {
                            continue;
                        }
                        Debug.Log($"Unique shader: {shaderLocation.PrimaryKey}");
                        uniqueShaderLocations.Add(shaderLocation);
                    }
                }
            }
            var asyncOp = Addressables.LoadAssetsAsync<Shader>(uniqueShaderLocations, null, false);
            asyncOp.Completed += handle =>
            {
                List<AddressableShaderInfo> previousAddressableShaders = new List<AddressableShaderInfo>(addressableShaders);
                addressableShaders.Clear();

                for (int i = 0; i < uniqueShaderLocations.Count; i++)
                {
                    IResourceLocation shaderLocation = uniqueShaderLocations[i];
                    Shader shader = handle.Result[i];
                    if (!shader || ignoredShaderDirectories.Any(x => shader.name.StartsWith(x)))
                    {
                        continue;
                    }
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader, out _, out long localId);
                    string primaryKey = shaderLocation.PrimaryKey;
                    ImportVirtualAsset(primaryKey, typeof(Shader).AssemblyQualifiedName, out string virtualAssetPath);
                    shader = AssetDatabase.LoadAssetAtPath<Shader>(virtualAssetPath);
                    if (!shader)
                    {
                        continue;
                    }
                    AddressableShaderInfo addressableShaderInfo = default;
                    int previousAddressableShaderIndex = previousAddressableShaders.FindIndex(info => info.primaryKey == primaryKey);
                    if (previousAddressableShaderIndex >= 0)
                    {
                        addressableShaderInfo = previousAddressableShaders[previousAddressableShaderIndex];
                        previousAddressableShaders.RemoveAt(previousAddressableShaderIndex);
                    }; 
                    addressableShaderInfo.primaryKey = primaryKey;
                    addressableShaderInfo.identifier = localId;
                    addressableShaderInfo.asset = shader;
                    addressableShaderInfo.materialsWithShader ??= new List<Material>();
                    addressableShaders.Add(addressableShaderInfo);
                }
                foreach (var remainingAddressableShaderInfo in previousAddressableShaders)
                {
                    DeleteVirtualAsset(remainingAddressableShaderInfo.primaryKey, typeof(Shader).AssemblyQualifiedName);
                }
                shaderInfoCache.Clear();
                EditorUtility.SetDirty(this);
            };
            return asyncOp;
        }

        public void RefreshAssets()
        {
            Debug.Log("Refreshing all assets..");
            AssetDatabase.DeleteAsset(GROUPS_DIRECTORY);
            RefreshAddressableShaders();
            for (int i = assetRequests.Count - 1; i >= 0; i--)
            {
                AssetRequestInfo assetRequest = assetRequests[i];
                ImportVirtualAsset(assetRequest.primaryKey, assetRequest.assemblyQualifiedTypeName, out string virtualAssetPath);
                Type assetType = AssetDatabase.GetMainAssetTypeAtPath(virtualAssetPath);
                if (assetType == null || assetType.AssemblyQualifiedName != assetRequest.assemblyQualifiedTypeName)
                {
                    RemoveAsset(assetRequest);
                }
            }
        }

        public void RebuildAssets()
        {
            Debug.Log("REBUILDING all assets..");
            AssetDatabase.DeleteAsset(VIRTUAL_ASSETS_DIRECTORY);
            RefreshAssets();
        }
    }
}