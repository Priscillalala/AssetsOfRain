using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AssetsOfRain.Editor.Util
{
    public static class ImportUtil
    {
        public static MonoScript FindScript(Type classType)
        {
            return AssetDatabase.FindAssets($"t:{nameof(MonoScript)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .SelectMany(x => AssetDatabase.LoadAllAssetsAtPath(x).OfType<MonoScript>())
                .FirstOrDefault(x => x.GetClass() == classType);
        }

        public static void SetScriptReference(Object asset, MonoScript monoScript)
        {
            const string SCRIPT_PROPERTY = "m_Script";

            using SerializedObject serializedAsset = new SerializedObject(asset);
            var scriptProperty = serializedAsset.FindProperty(SCRIPT_PROPERTY);
            if (scriptProperty != null)
            {
                scriptProperty.objectReferenceInstanceIDValue = monoScript.GetInstanceID();
                scriptProperty.objectReferenceValue = monoScript;
                serializedAsset.ApplyModifiedProperties();
            }
        }

        public static Texture2D DuplicateCompressedTexture(Texture2D srcTex)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(srcTex.width, srcTex.height);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(srcTex, renderTex);
            Texture2D outputTex = new Texture2D(srcTex.width, srcTex.height)
            {
                alphaIsTransparency = srcTex.alphaIsTransparency
            };
            outputTex.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            outputTex.Apply(false, true);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return outputTex;
        }

        // There MUST be a better way
        public static Cubemap DuplicateCompressedCubemap(Cubemap srcCubemap)
        {
            Cubemap outputCubemap = new Cubemap(srcCubemap.width, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.None);

            RenderTexture renderTex = RenderTexture.GetTemporary(srcCubemap.width, srcCubemap.height, 0, GraphicsFormat.R8G8B8A8_SRGB);
            RenderTexture previous = RenderTexture.active;

            Texture2D intermediaryTex = new Texture2D(srcCubemap.width, srcCubemap.height, srcCubemap.format, false);

            for (var face = CubemapFace.PositiveX; face <= CubemapFace.NegativeZ; face++)
            {
                Graphics.CopyTexture(srcCubemap, (int)face, 0, intermediaryTex, 0, 0);
                Graphics.Blit(intermediaryTex, renderTex);
                var asyncGPUReadback = AsyncGPUReadback.Request(renderTex, 0);
                asyncGPUReadback.WaitForCompletion();
                outputCubemap.SetPixelData(asyncGPUReadback.GetData<byte>(), 0, face);
            }

            outputCubemap.Apply(false, true);

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);

            Object.DestroyImmediate(intermediaryTex);

            return outputCubemap;
        }

        public static Shader GetDummyShader(Shader srcShader, AssetImportContext ctx)
        {
            string dummyShaderSource = $@"Shader ""{srcShader.name}"" 
{{
    SubShader 
    {{
        ZTest Never
        Pass {{ }}
    }}
    Fallback Off
}}";

            Shader dummyShader = ShaderUtil.CreateShaderAsset(ctx, dummyShaderSource, false);

            using SerializedObject seralializedSrcShader = new SerializedObject(srcShader);
            using SerializedObject serializedDummyShader = new SerializedObject(dummyShader);

            serializedDummyShader.CopyFromSerializedProperty(seralializedSrcShader.FindProperty("m_ParsedForm.m_PropInfo"));
            serializedDummyShader.CopyFromSerializedProperty(seralializedSrcShader.FindProperty("m_ParsedForm.m_KeywordNames"));
            serializedDummyShader.CopyFromSerializedProperty(seralializedSrcShader.FindProperty("m_ParsedForm.m_KeywordFlags"));
            serializedDummyShader.ApplyModifiedProperties();

            return dummyShader;
        }
    }
}
