using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AssetsOfRain.Editor.VirtualAssets.VirtualShaders
{
    // When materials reference a virtual shader asset, upgrade them to a loaded addressable shader that will display correctly
    public class MaterialPostProcessor : AssetPostprocessor
    {
        public static bool enabled = false;

        static MaterialPostProcessor()
        {
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
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
                OnMaterialPropertiesChanged(material, true);
            }
            if (didDomainReload)
            {
                // Without this delay, changes were failing to apply to virtual materials
                EditorApplication.delayCall += UpdateAfterDomainReload;
            }
        }

        public static void UpdateAfterDomainReload()
        {
            static string ResolvePersistentShaderPath(Material material)
            {
                int instanceId = material.GetInstanceID();
                if (material.shader && material.shader.isSupported)
                {
                    // When the project is first loaded, materials will reference valid virtual shader assets
                    VirtualShaderDataStorage.instance.materialToShaderAsset[instanceId] = material.shader;
                    return AssetDatabase.GetAssetPath(material.shader);
                }
                else if (VirtualShaderDataStorage.instance.materialToShaderAsset.TryGetValue(instanceId, out Shader shaderAsset))
                {
                    // After domain reloads, we need to use the virtual shader asset references we saved
                    return AssetDatabase.GetAssetPath(shaderAsset);
                }
                return null;
            }

            var allMaterialsByShader = AssetDatabase.FindAssets($"glob:\"(*.mat|*.{VirtualAddressableAssetImporter.EXTENSION})\" a:assets")//($"t:{nameof(Material)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<Material>()
                .GroupBy(ResolvePersistentShaderPath);

            foreach (var materialsWithShaderGroup in allMaterialsByShader)
            {
                if (string.IsNullOrEmpty(materialsWithShaderGroup.Key) || AssetImporter.GetAtPath(materialsWithShaderGroup.Key) is not VirtualAddressableAssetImporter importer || !typeof(Shader).IsAssignableFrom(importer.request.AssetType))
                {
                    continue;
                }
                var loadShaderOp = Addressables.LoadAssetAsync<Shader>(importer.request.AssetLocation);
                loadShaderOp.Completed += handle =>
                {
                    Shader shader = handle.Result;
                    foreach (Material material in materialsWithShaderGroup)
                    {
                        if (material)
                        {
                            if (enabled)
                            {
                                material.shader = shader;
                            }
                        }
                    }
                };
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
                VirtualShaderDataStorage.instance.materialToShaderAsset[material.GetInstanceID()] = shader;
                var loadShaderOp = Addressables.LoadAssetAsync<Shader>(importer.request.AssetLocation);
                loadShaderOp.Completed += handle =>
                {
                    Shader shader = handle.Result;
                    if (material && shader)
                    {
                        if (enabled)
                        {
                            material.shader = shader;
                        }
                    }
                };
            }
            else
            {
                // This will trigger a reimport and invoke OnMaterialPropertiesChanged again
                // The asset on disk needs to be saved with a reference to the virtual shader asset before we re-assign the material's shader
                AssetDatabase.SaveAssetIfDirty(material);
            }
        }
    }
}