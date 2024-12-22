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
    public class MaterialPostprocessor : AssetPostprocessor
    {
        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (!AssetsOfRainManager.TryGetInstance(out var manager))
            {
                return;
            }
            foreach (Material material in importedAssets.SelectMany(x => AssetDatabase.LoadAllAssetsAtPath(x).OfType<Material>()))
            {
                Debug.Log($"OnPostprocessAllAssets: imported material {material.name}");
                manager.OnMaterialPropertiesChanged(material);
            }
            if (!didDomainReload)
            {
                return;
            }
            Debug.Log("OnPostprocessAllAssets: reloading shaders");
            foreach (var addressableShaderInfo in manager.addressableShaders)
            {
                Shader shader = Addressables.LoadAssetAsync<Shader>(addressableShaderInfo.primaryKey).WaitForCompletion();
                for (int i = addressableShaderInfo.materialsWithShader.Count - 1; i >= 0; i--)
                {
                    Material materialWithShader = addressableShaderInfo.materialsWithShader[i];
                    if (materialWithShader != null)
                    {
                        Debug.Log($"OnPostprocessAllAssets found {materialWithShader.name}");
                        if (materialWithShader.shader == null || materialWithShader.shader == addressableShaderInfo.asset || materialWithShader.shader == shader || !materialWithShader.shader.isSupported)
                        {
                            Debug.Log($"OnPostprocessAllAssets set {materialWithShader.name} shader");
                            materialWithShader.shader = shader;
                            continue;
                        }
                    }
                    addressableShaderInfo.materialsWithShader.RemoveAt(i);
                    EditorUtility.SetDirty(manager);
                }
            }
        }
    }
}