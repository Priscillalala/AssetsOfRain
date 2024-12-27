using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

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

        public static void SetScriptReference(Object asset, int scriptInstanceID)
        {
            const string SCRIPT_PROPERTY = "m_Script";

            using SerializedObject serializedAsset = new SerializedObject(asset);
            var scriptProperty = serializedAsset.FindProperty(SCRIPT_PROPERTY);
            if (scriptProperty != null)
            {
                scriptProperty.objectReferenceInstanceIDValue = scriptInstanceID;
            }
            serializedAsset.ApplyModifiedProperties();
        }

        public static Texture2D DuplicateCompressedTexture(Texture2D srcTex)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(srcTex.width, srcTex.height);//, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(srcTex, renderTex);
            //RenderTexture.active = renderTex;
            Texture2D outputTex = new Texture2D(srcTex.width, srcTex.height);
            outputTex.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            outputTex.Apply(false, true);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            outputTex.alphaIsTransparency = srcTex.alphaIsTransparency;
            return outputTex;
        }
    }
}
