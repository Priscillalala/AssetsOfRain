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
        private static readonly Dictionary<int, AddressableShaderInfo> shaderInfoCache = new Dictionary<int, AddressableShaderInfo>();

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

        public static string GetVirtualAssetPath(string primaryKey)
        {
            return Path.Combine(VIRTUAL_ASSETS_DIRECTORY, Path.ChangeExtension(primaryKey, VirtualAddressableAssetImporter.EXTENSION));
        }

        public void OnEnable()
        {
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        public void OnDisable()
        {
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
        }

        private void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            Debug.Log($"OnChangesPublished ({stream.length}) changes");
            for (int i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.CreateAssetObject:
                        stream.GetCreateAssetObjectEvent(i, out var createAssetObjectEventArgs);
                        if (EditorUtility.InstanceIDToObject(createAssetObjectEventArgs.instanceId) is Material createdMaterial)
                        {
                            OnMaterialPropertiesChanged(createdMaterial);
                        }
                        break;
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        stream.GetChangeAssetObjectPropertiesEvent(i, out var changeAssetObjectPropertiesEventArgs);
                        if (EditorUtility.InstanceIDToObject(changeAssetObjectPropertiesEventArgs.instanceId) is Material changedMaterial)
                        {
                            OnMaterialPropertiesChanged(changedMaterial);
                        }
                        break;
                }
            }
        }

        public void OnMaterialPropertiesChanged(Material material)
        {
            Shader shader = material.shader;
            if (shader == null)
            {
                return;
            }
            //Debug.Log($"Properties changed at {AssetDatabase.GUIDToAssetPath(data.guid)}");
            int shaderInstanceId = material.shader.GetInstanceID();
            if (!shaderInfoCache.TryGetValue(shaderInstanceId, out var addressableShaderInfo))
            {
                addressableShaderInfo = addressableShaders.FirstOrDefault(x => x.asset == shader);
                shaderInfoCache.Add(shaderInstanceId, addressableShaderInfo);
            }
            if (addressableShaderInfo.materialsWithShader != null && !addressableShaderInfo.materialsWithShader.Contains(material))
            {
                Debug.Log($"Adding material {material.name} to addressable shader list {addressableShaderInfo.primaryKey}");
                foreach (var otherAddressableShaderInfo in addressableShaders)
                {
                    otherAddressableShaderInfo.materialsWithShader?.Remove(material);
                }
                addressableShaderInfo.materialsWithShader.Add(material);
                EditorUtility.SetDirty(this);
            }
            if (addressableShaderInfo.asset != null)
            {
                Debug.Log($"Setting material {material.name} to use shader {addressableShaderInfo.asset.name} temporarily");
                AssetDatabase.SaveAssetIfDirty(material);
                material.shader = Addressables.LoadAssetAsync<Shader>(addressableShaderInfo.primaryKey).WaitForCompletion();
            }
        }

        public bool RequestAsset(AssetRequestInfo assetRequest, out string virtualAssetPath, bool recordRequest = true)
        {
            Debug.Log($"RequestAsset: {assetRequest.primaryKey}");

            if (recordRequest && !assetRequests.Contains(assetRequest))
            {
                assetRequests.Add(assetRequest);
                EditorUtility.SetDirty(this);
            }

            virtualAssetPath = GetVirtualAssetPath(assetRequest.primaryKey);
            if (!File.Exists(virtualAssetPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(virtualAssetPath));
                File.Create(virtualAssetPath).Close();
                AssetDatabase.ImportAsset(virtualAssetPath);
            }
            if (AssetImporter.GetAtPath(virtualAssetPath) is not VirtualAddressableAssetImporter importer)
            {
                return false;
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

            return true;
        }

        public bool RemoveAsset(AssetRequestInfo assetRequest)
        {
            if (assetRequests.Remove(assetRequest))
            {
                EditorUtility.SetDirty(this);
            }
            string virtualAssetPath = GetVirtualAssetPath(assetRequest.primaryKey);
            return AssetDatabase.DeleteAsset(virtualAssetPath);
        }

        public void RebuildAddressableShaders()
        {
            Debug.Log("LoadAddressableShaders");
            HashSet<string> foundShaderKeys = new HashSet<string>();
            List<AddressableShaderInfo> oldAddressableShaders = new List<AddressableShaderInfo>(addressableShaders);
            addressableShaders.Clear();
            foreach (IResourceLocator resourceLocator in Addressables.ResourceLocators)
            {
                foreach (var key in resourceLocator.Keys)
                {
                    if (!resourceLocator.Locate(key, typeof(Shader), out IList<IResourceLocation> locations))
                    {
                        continue;
                    }
                    var shaderLocation = locations.FirstOrDefault();
                    string primaryKey = shaderLocation.PrimaryKey;
                    if (shaderLocation == null || !foundShaderKeys.Add(primaryKey))
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
                    RequestAsset(shaderLocation, out string virtualAssetPath, false);
                    shader = AssetDatabase.LoadAssetAtPath<Shader>(virtualAssetPath);
                    Debug.Log($"Is supported? {shader.isSupported}");
                    if (!shader)
                    {
                        continue;
                    }
                    AddressableShaderInfo addressableShaderInfo = oldAddressableShaders.FirstOrDefault(x => x.primaryKey == primaryKey);
                    addressableShaderInfo.primaryKey = shaderLocation.PrimaryKey;
                    addressableShaderInfo.identifier = localId;
                    addressableShaderInfo.asset = shader;
                    addressableShaderInfo.materialsWithShader ??= new List<Material>();
                    addressableShaders.Add(addressableShaderInfo);
                }
            }
            shaderInfoCache.Clear();
            EditorUtility.SetDirty(this);
        }

        public void RebuildAssets()
        {
            Debug.Log("Rebuilding all assets..");
            AssetDatabase.DeleteAsset(VIRTUAL_ASSETS_DIRECTORY);
            AssetDatabase.DeleteAsset(GROUPS_DIRECTORY);
            RebuildAddressableShaders();
            foreach (var assetRequest in assetRequests)
            {
                RequestAsset(assetRequest, out _, false);
            }
        }
    }
}