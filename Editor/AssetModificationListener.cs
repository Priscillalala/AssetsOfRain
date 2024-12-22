using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace AssetsOfRain.Editor
{
    public class AssetModificationListener : AssetModificationProcessor
    {
        public static string[] OnWillSaveAssets(string[] assetPaths)
        {
            Debug.Log("OnWillSaveAssets");
            foreach (var assetPath in assetPaths)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material == null)
                {
                    continue;
                }
                Debug.Log($"OnWillSaveAssets looked at {material.name}");
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material.shader)))
                {
                    continue;
                }
                Debug.Log($"OnWillSaveAssets is worried about {material.name}");
                if (!AssetsOfRainManager.TryGetInstance(out var manager))
                {
                    continue;
                }
                var addressableShaderInfo = manager.addressableShaders.FirstOrDefault(x => x.materialsWithShader.Contains(material));
                if (addressableShaderInfo.asset != null)
                {
                    Debug.Log($"OnWillSaveAssets Set material {material.name} to use {addressableShaderInfo.primaryKey}");
                    material.shader = addressableShaderInfo.asset;
                    EditorApplication.delayCall += delegate
                    {
                        if (material)
                        {
                            material.shader = Addressables.LoadAssetAsync<Shader>(addressableShaderInfo.primaryKey).WaitForCompletion();
                            Debug.Log($"OnWillSaveAssets Set material {material.name} to use working shader again");
                        }
                    };
                }
            }
            return assetPaths;
        }
    }
}