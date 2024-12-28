using AssetsOfRain.Editor.Util;
using AssetsOfRain.Editor.VirtualAssets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        const string MANAGER_FILE_PATH = AssetsOfRain.DATA_DIRECTORY + "/AssetsOfRainManager.asset";
        const string VIRTUAL_ASSETS_DIRECTORY = AssetsOfRain.DATA_DIRECTORY + "/VirtualAssets";
        const string VIRTUAL_SHADERS_DIRECTORY = AssetsOfRain.DATA_DIRECTORY + "/VirtualShaders";
        const string GROUPS_DIRECTORY = AssetsOfRain.DATA_DIRECTORY + "/Groups";

        public VirtualAddressableAssetCollection virtualShaders = new VirtualAddressableAssetCollection(VIRTUAL_SHADERS_DIRECTORY, GROUPS_DIRECTORY);
        public VirtualAddressableAssetCollection virtualAssets = new VirtualAddressableAssetCollection(VIRTUAL_ASSETS_DIRECTORY, GROUPS_DIRECTORY);
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

        public void RefreshVirtualShaders()
        {
            Debug.Log("RefreshVirtualShaders");

            var previousShaderRequests = virtualShaders.GetAssetRequests();

            HashSet<string> foundShaderKeys = new HashSet<string>();
            List<IResourceLocation> validShaderLocations = new List<IResourceLocation>();
            foreach (IResourceLocation shaderLocation in Addressables.ResourceLocators.SelectMany(x => x.AllLocationsOfType(typeof(Shader))))
            {
                if (shaderLocation == null || !foundShaderKeys.Add(shaderLocation.PrimaryKey))
                {
                    continue;
                }
                // temp
                if (shaderLocation.PrimaryKey != "RoR2/Base/Shaders/HGStandard.shader" && shaderLocation.PrimaryKey != "RoR2/Base/Shaders/HGCloudRemap.shader")
                {
                    continue;
                }
                Shader shader = Addressables.LoadAssetAsync<Shader>(shaderLocation).WaitForCompletion();
                if (!shader || ignoredShaderDirectories.Any(x => shader.name.StartsWith(x)))
                {
                    continue;
                }
                Debug.Log($"Valid shader: {shaderLocation.PrimaryKey}");
                validShaderLocations.Add(shaderLocation);
            }

            var newShaderRequests = validShaderLocations.Select(x => new SerializableAssetRequest
            {
                AssetLocation = x,
            }).ToList();

            foreach (var previousShaderRequest in previousShaderRequests)
            {
                if (!newShaderRequests.Contains(previousShaderRequest) && !virtualShaders.TryMoveVirtualAsset(previousShaderRequest, validShaderLocations))
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
            Debug.Log("Refreshing all assets..");
            AssetDatabase.DeleteAsset(GROUPS_DIRECTORY);
            RefreshVirtualShaders();

            var assetRequests = virtualAssets.GetAssetRequests();
            foreach (var assetRequest in assetRequests)
            {
                virtualAssets.ImportVirtualAsset(assetRequest);
                if (!virtualAssets.VirtualAssetExists(assetRequest))
                {
                    var allowedAssetLocations = Addressables.ResourceLocators.SelectMany(x => x.AllLocationsOfType(assetRequest.AssetType));
                    if (!virtualAssets.TryMoveVirtualAsset(assetRequest, allowedAssetLocations))
                    {
                        virtualAssets.DeleteVirtualAsset(assetRequest);
                    }
                }
            }

            EditorUtility.SetDirty(this);
        }

        public void DeleteVirtualAssets()
        {
            AssetDatabase.DeleteAsset(GROUPS_DIRECTORY);
            AssetDatabase.DeleteAsset(VIRTUAL_ASSETS_DIRECTORY);
            AssetDatabase.DeleteAsset(VIRTUAL_SHADERS_DIRECTORY);
        }
    }
}