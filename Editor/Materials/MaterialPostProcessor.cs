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

        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            foreach (Material material in importedAssets.SelectMany(x => AssetDatabase.LoadAllAssetsAtPath(x).OfType<Material>()))
            {
                Debug.Log($"OnPostprocessAllAssets: imported material {material.name}");
                OnMaterialPropertiesChanged(material);
            }
            if (!didDomainReload)
            {
                return;
            }
            Debug.Log("OnPostprocessAllAssets: reloading shaders");

            var allMaterialsByShader = AssetDatabase.FindAssets($"t:{nameof(Material)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<Material>()
                .GroupBy(x => AssetDatabase.GetAssetPath(x.shader));
            foreach (var materialsWithShaderGroup in allMaterialsByShader)
            {
                if (AssetImporter.GetAtPath(materialsWithShaderGroup.Key) is not VirtualAddressableAssetImporter importer)
                {
                    continue;
                }
                Shader shader = Addressables.LoadAssetAsync<Shader>(importer.request.AssetLocation).WaitForCompletion();
                foreach (Material material in materialsWithShaderGroup)
                {
                    Debug.Log($"OnPostprocessAllAssets set {material.name} shader");
                    material.shader = shader;
                }
            }
        }

        public static void OnMaterialPropertiesChanged(Material material)
        {
            Shader shader = material.shader;
            if (shader == null)
            {
                return;
            }
            string shaderAssetPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(shaderAssetPath) || AssetImporter.GetAtPath(shaderAssetPath) is not VirtualAddressableAssetImporter importer)
            {
                return;
            }
            Debug.Log($"Setting material {material.name} to use shader {importer.request.primaryKey} temporarily");
            AssetDatabase.SaveAssetIfDirty(material);
            material.shader = Addressables.LoadAssetAsync<Shader>(importer.request.AssetLocation).WaitForCompletion();
        }
    }
}