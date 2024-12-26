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

namespace AssetsOfRain.Editor.Materials
{
    public class MaterialSaveProcessor : AssetModificationProcessor
    {
        public static string[] OnWillSaveAssets(string[] assetPaths)
        {
            foreach (var assetPath in assetPaths)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material == null)
                {
                    continue;
                }
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
                if (string.IsNullOrEmpty(serializableShaderAssetPath) || AssetImporter.GetAtPath(serializableShaderAssetPath) is not VirtualAddressableAssetImporter importer)
                {
                    continue;
                }
                Debug.Log($"OnWillSaveAssets Set material {material.name} to use {importer.request.primaryKey}");
                material.shader = serializableShader;
            }
            return assetPaths;
        }
    }
}