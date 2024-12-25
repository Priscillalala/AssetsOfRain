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
                using SerializedObject serializedMaterial = new SerializedObject(material);
                Shader serializedShader = serializedMaterial.FindProperty("m_Shader").objectReferenceValue as Shader;
                string serializedShaderAssetPath = AssetDatabase.GetAssetPath(serializedShader);
                if (string.IsNullOrEmpty(serializedShaderAssetPath) || AssetImporter.GetAtPath(serializedShaderAssetPath) is not VirtualAddressableAssetImporter importer)
                {
                    continue;
                }
                Debug.Log($"OnWillSaveAssets Set material {material.name} to use {importer.primaryKey}");
                material.shader = serializedShader;
                string primaryKey = importer.primaryKey;
                EditorApplication.delayCall += delegate
                {
                    if (material)
                    {
                        material.shader = Addressables.LoadAssetAsync<Shader>(primaryKey).WaitForCompletion();
                        Debug.Log($"OnWillSaveAssets Set material {material.name} to use working shader again");
                    }
                };
            }
            return assetPaths;
        }
    }
}