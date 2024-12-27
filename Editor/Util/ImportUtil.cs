using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace AssetsOfRain.Editor.Util
{
    public static class ImportUtil
    {
        public static int FindScriptInstanceID(Type classType)
        {
            MonoScript monoScript = AssetDatabase.FindAssets($"t:{nameof(MonoScript)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<MonoScript>()
                .FirstOrDefault(x => x.GetClass() == classType);
            return monoScript ? monoScript.GetInstanceID() : 0;
        }
    }
}
