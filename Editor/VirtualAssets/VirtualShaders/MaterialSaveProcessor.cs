using AssetsOfRain.Editor.Util;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetsOfRain.Editor.VirtualAssets.VirtualShaders
{
    // If a material would be saved with a reference to a loaded addressable shader, give it
    // a virtual shader asset to reference instead. No broken guids!
    public class MaterialSaveProcessor : AssetModificationProcessor
    {
        public static string[] OnWillSaveAssets(string[] assetPaths)
        {
            foreach (Material material in MaterialSearchUtil.GetMaterialAssets(assetPaths))
            {
                Shader shader = material.shader;
                // Loaded addressable shaders have no asset path
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(shader)))
                {
                    continue;
                }
                Shader shaderAsset = Shader.Find(shader.name);
                if (shaderAsset == shader)
                {
                    continue;
                }
                string shaderAssetPath = AssetDatabase.GetAssetPath(shaderAsset);
                if (string.IsNullOrEmpty(shaderAssetPath) || AssetImporter.GetAtPath(shaderAssetPath) is not VirtualAddressableAssetImporter)
                {
                    continue;
                }
                material.shader = shaderAsset;
            }
            return assetPaths;
        }
    }
}