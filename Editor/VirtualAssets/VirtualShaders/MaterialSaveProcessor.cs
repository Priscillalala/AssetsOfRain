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
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace AssetsOfRain.Editor.VirtualAssets.VirtualShaders
{
    public class MaterialSaveProcessor : AssetModificationProcessor
    {
        public static string[] OnWillSaveAssets(string[] assetPaths)
        {
            foreach (Material material in assetPaths.SelectMany(x => AssetDatabase.LoadAllAssetsAtPath(x).OfType<Material>()))
            {
                Debug.Log($"OnWillSaveAssets looked at {material.name}");
                Shader shader = material.shader;
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(shader)))
                {
                    continue;
                }
                Debug.Log($"OnWillSaveAssets is worried about {material.name}");

                Shader serializableShader = Shader.Find(shader.name);
                if (serializableShader == shader)
                {
                    continue;
                }
                Debug.Log($"serializableShader: {serializableShader}");
                string serializableShaderAssetPath = AssetDatabase.GetAssetPath(serializableShader);
                Debug.Log($"serializedShaderAssetPath: {serializableShaderAssetPath}");
                if (string.IsNullOrEmpty(serializableShaderAssetPath) || AssetImporter.GetAtPath(serializableShaderAssetPath) is not VirtualAddressableAssetImporter)
                {
                    continue;
                }
                Debug.Log($"OnWillSaveAssets Set material {material.name}");
                material.shader = serializableShader;
            }
            return assetPaths;
        }
    }
}