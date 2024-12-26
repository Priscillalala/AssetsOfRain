using AssetsOfRain.Editor.VirtualAssets;
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
    public class MaterialPostProcessor : AssetPostprocessor
    {
        static MaterialPostProcessor()
        {
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
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
                            OnMaterialPropertiesChanged(createdMaterial, true);
                        }
                        break;
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        stream.GetChangeAssetObjectPropertiesEvent(i, out var changeAssetObjectPropertiesEventArgs);
                        if (EditorUtility.InstanceIDToObject(changeAssetObjectPropertiesEventArgs.instanceId) is Material changedMaterial)
                        {
                            OnMaterialPropertiesChanged(changedMaterial, false);
                        }
                        break;
                }
            }
        }

        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            foreach (Material material in importedAssets.SelectMany(x => AssetDatabase.LoadAllAssetsAtPath(x).OfType<Material>()))
            {
                Debug.Log($"OnPostprocessAllAssets: imported material {material.name}");
                OnMaterialPropertiesChanged(material, true);
            }
            if (!didDomainReload)
            {
                return;
            }
            EditorApplication.delayCall += UpdateAfterDomainReload;
        }

        public static void UpdateAfterDomainReload()
        {
            Debug.Log("OnPostprocessAllAssets: reloading shaders");

            static string GetShaderAssetPath(Material material)
            {
                if (material.shader && material.shader.isSupported)
                {
                    return AssetDatabase.GetAssetPath(material.shader);
                }
                if (MaterialDataStorage.instance.materialToPersistentShader.TryGetValue(material.GetInstanceID(), out Shader persistentShader))
                {
                    Debug.LogWarning($"Found persistent shader for {material.name}: shader is {persistentShader.name}");
                    return AssetDatabase.GetAssetPath(persistentShader);
                }
                return null;
            }

            var allMaterialsByShader = AssetDatabase.FindAssets($"t:{nameof(Material)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<Material>()
                .GroupBy(GetShaderAssetPath);
            foreach (var materialsWithShaderGroup in allMaterialsByShader)
            {
                if (string.IsNullOrEmpty(materialsWithShaderGroup.Key) || AssetImporter.GetAtPath(materialsWithShaderGroup.Key) is not VirtualAddressableAssetImporter importer || !typeof(Shader).IsAssignableFrom(importer.request.AssetType))
                {
                    continue;
                }
                /*Shader shader = Addressables.LoadAssetAsync<Shader>(importer.request.AssetLocation).WaitForCompletion();
                foreach (Material material in materialsWithShaderGroup)
                {
                    Debug.Log($"OnPostprocessAllAssets set {material.name} shader");
                    MaterialDataStorage.instance.materialToPersistentShader[material.GetInstanceID()] = AssetDatabase.LoadAssetAtPath<Shader>(materialsWithShaderGroup.Key);
                    material.shader = shader;
                }*/
                var loadShaderOp = Addressables.LoadAssetAsync<Shader>(importer.request.AssetLocation);
                loadShaderOp.Completed += handle =>
                {
                    Shader shader = handle.Result;
                    foreach (Material material in materialsWithShaderGroup)
                    {
                        if (material)
                        {
                            Debug.Log($"OnPostprocessAllAssets set {material.name} shader");
                            MaterialDataStorage.instance.materialToPersistentShader[material.GetInstanceID()] = AssetDatabase.LoadAssetAtPath<Shader>(materialsWithShaderGroup.Key);
                            material.shader = shader;
                        }
                    }
                };
                /*EditorApplication.delayCall += delegate
                {
                    
                };*/
            }
        }

        public static void OnMaterialPropertiesChanged(Material material, bool wasSaved)
        {
            Shader shader = material.shader;
            if (shader == null)
            {
                return;
            }
            string shaderAssetPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(shaderAssetPath) || AssetImporter.GetAtPath(shaderAssetPath) is not VirtualAddressableAssetImporter importer || !typeof(Shader).IsAssignableFrom(importer.request.AssetType))
            {
                return;
            }
            if (wasSaved)
            {
                Debug.Log($"Setting material {material.name} to use shader {importer.request.primaryKey} temporarily");
                MaterialDataStorage.instance.materialToPersistentShader[material.GetInstanceID()] = shader;
                material.shader = Addressables.LoadAssetAsync<Shader>(importer.request.AssetLocation).WaitForCompletion();
            }
            else
            {
                Debug.Log($"saving material {material.name} to reimport");
                AssetDatabase.SaveAssetIfDirty(material);
            }
        }
    }
}