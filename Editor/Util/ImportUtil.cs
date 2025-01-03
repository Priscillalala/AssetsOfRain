using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetsOfRain.Editor.Util
{
    public static class ImportUtil
    {
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
