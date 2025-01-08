using AssetsOfRain.Editor.VirtualAssets;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetsOfRain.Editor.Util
{
    public static class MaterialSearchUtil
    {
        public static IEnumerable<Material> GetAllMaterialAssets()
        {
            return AssetDatabase.FindAssets($"glob:\"(*.mat|*{VirtualAddressableAssetImporter.EXTENSION})\" a:assets")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .SelectMany(x => AssetDatabase.LoadAllAssetsAtPath(x).OfType<Material>());
        }

        public static IEnumerable<Material> GetMaterialAssets(IEnumerable<string> assetPaths)
        {
            return assetPaths
                .Where(x => Path.GetExtension(x) is ".mat" or VirtualAddressableAssetImporter.EXTENSION)
                .SelectMany(x => AssetDatabase.LoadAllAssetsAtPath(x).OfType<Material>());
        }
    }
}
