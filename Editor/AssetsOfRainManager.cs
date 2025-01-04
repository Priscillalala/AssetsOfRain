using AssetsOfRain.Editor.Util;
using AssetsOfRain.Editor.VirtualAssets;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace AssetsOfRain.Editor
{
    public class AssetsOfRainManager : ScriptableObject
    {
        const string MANAGER_FILE_PATH = AssetsOfRain.DATA_DIRECTORY + "/AssetsOfRainManager.asset";
        const string VIRTUAL_ASSETS_DIRECTORY = AssetsOfRain.DATA_DIRECTORY + "/VirtualAssets";
        const string VIRTUAL_SHADERS_DIRECTORY = AssetsOfRain.DATA_DIRECTORY + "/VirtualShaders";

        public VirtualAddressableAssetCollection virtualShaders = new VirtualAddressableAssetCollection(VIRTUAL_SHADERS_DIRECTORY);
        public VirtualAddressableAssetCollection virtualAssets = new VirtualAddressableAssetCollection(VIRTUAL_ASSETS_DIRECTORY);
        // RoR2 uses optimized versions of shaders for the Switch which are never relevent to modding or referenced on PC builds
        public string[] ignoredShaderDirectories = new[]
        {
            "Hopoo Games/Optimized/Switch/",
        };

        private static AssetsOfRainManager instance;

        public static bool TryGetInstance(out AssetsOfRainManager manager)
        {
            if (!instance)
            {
                instance = AssetDatabase.LoadAssetAtPath<AssetsOfRainManager>(MANAGER_FILE_PATH);
            }
            return (manager = instance) != null;
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
            instance.RefreshVirtualShaders();
            return instance;
        }

        // Shaders are handled separately and every viable addressable shader is always given a virtual asset
        public void RefreshVirtualShaders()
        {
            var previousShaderRequests = virtualShaders.GetAssetRequests();

            HashSet<string> foundShaderKeys = new HashSet<string>();
            List<IResourceLocation> validShaderLocations = new List<IResourceLocation>();
            foreach (IResourceLocation shaderLocation in Addressables.ResourceLocators.SelectMany(x => x.AllLocationsOfType(typeof(Shader))))
            {
                if (shaderLocation == null || !foundShaderKeys.Add(shaderLocation.PrimaryKey))
                {
                    continue;
                }
                Shader shader = Addressables.LoadAssetAsync<Shader>(shaderLocation).WaitForCompletion();
                if (!shader || ignoredShaderDirectories.Any(x => shader.name.StartsWith(x)))
                {
                    continue;
                }
                string shaderAssetPath = AssetDatabase.GetAssetPath(Shader.Find(shader.name));
                if (!string.IsNullOrEmpty(shaderAssetPath) && !shaderAssetPath.StartsWith(VIRTUAL_SHADERS_DIRECTORY))
                {
                    continue;
                }
                validShaderLocations.Add(shaderLocation);
            }

            var newShaderRequests = validShaderLocations.Select(x => new SerializableAssetRequest
            {
                AssetLocation = x,
            }).ToList();

            foreach (var previousShaderRequest in previousShaderRequests)
            {
                if (!newShaderRequests.Contains(previousShaderRequest) && !virtualShaders.TryMoveVirtualAsset(previousShaderRequest, validShaderLocations, out _))
                {
                    virtualShaders.DeleteVirtualAsset(previousShaderRequest);
                }
            }

            foreach (var newShaderRequest in newShaderRequests)
            {
                virtualShaders.ImportVirtualAsset(newShaderRequest);
            }

            EditorUtility.SetDirty(this);
        }

        public void RefreshVirtualAssets()
        {
            RefreshVirtualShaders();

            var assetRequests = virtualAssets.GetAssetRequests();
            foreach (var assetRequest in assetRequests)
            {
                virtualAssets.ImportVirtualAsset(assetRequest);
                if (!virtualAssets.VirtualAssetExists(assetRequest))
                {
                    var allowedAssetLocations = Addressables.ResourceLocators.SelectMany(x => x.AllLocationsOfType(assetRequest.AssetType));
                    if (virtualAssets.TryMoveVirtualAsset(assetRequest, allowedAssetLocations, out var newAssetRequest))
                    {
                        Debug.LogWarning($"Asset {assetRequest.primaryKey} was moved to {newAssetRequest.primaryKey}");
                    }
                    else
                    {
                        Debug.LogWarning($"Asset {assetRequest.primaryKey} is no longer available!");
                        virtualAssets.DeleteVirtualAsset(assetRequest, false);
                    }
                }
            }

            EditorUtility.SetDirty(this);
        }

        public void DeleteVirtualAssets()
        {
            AssetDatabase.DeleteAsset(VIRTUAL_ASSETS_DIRECTORY);
            AssetDatabase.DeleteAsset(VIRTUAL_SHADERS_DIRECTORY);
        }
    }
}