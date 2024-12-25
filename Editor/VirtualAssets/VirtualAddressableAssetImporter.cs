using System;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using System.Linq;
using System.Reflection;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using UnityEditorInternal;
using UnityEngine.Experimental.Rendering;
using AssetsOfRain.Editor.Util;

namespace AssetsOfRain.Editor.VirtualAssets
{
    [ScriptedImporter(0, EXTENSION)]
    public class VirtualAddressableAssetImporter : ScriptedImporter
    {
        public const string EXTENSION = "virtualaa";

        public string primaryKey;
        public string assemblyQualifiedTypeName;

        [Serializable]
        public struct Result
        {
            public Object asset;
            public long identifier;
        }

        public List<Result> results = new List<Result>();

        public override void OnImportAsset(AssetImportContext ctx)
        {
            if (string.IsNullOrEmpty(primaryKey))
            {
                return;
            }

            Type assetType = Type.GetType(assemblyQualifiedTypeName);

            IResourceLocation assetLocation = Addressables.LoadResourceLocationsAsync(primaryKey, assetType).WaitForCompletion().FirstOrDefault();
            AsyncOperationHandle asyncOp = Addressables.ResourceManager.ProvideResource(assetLocation, assetType, true);
            Object asset = (Object)asyncOp.WaitForCompletion();
            if (!asset)
            {
                Debug.LogWarning("Importer: asset is null");
                return;
            }

            results.Clear();
            asset = CollectDependenciesRecursive(asset, ctx, new HashSet<int>(), new Dictionary<int, Object>());
            asset.hideFlags = HideFlags.NotEditable;
            ctx.SetMainObject(asset);
        }

        public Object CollectDependenciesRecursive(Object asset, AssetImportContext ctx, HashSet<int> objectsInHierarchy, Dictionary<int, Object> assetRepresentations)
        {
            Debug.Log($"Visited asset {asset.name} ({asset.GetType().Name})");
            int assetInstanceId = asset.GetInstanceID();
            if (objectsInHierarchy.Contains(assetInstanceId))
            {
                assetRepresentations[assetInstanceId] = asset;
            }
            else
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long localId);
                Object assetRepresentation = GetAssetRepresentation(asset, localId, ctx, out bool newRepresentation);
                assetRepresentations[assetInstanceId] = assetRepresentation;
                assetRepresentations[assetRepresentation.GetInstanceID()] = assetRepresentation;
                if (!newRepresentation)
                {
                    return assetRepresentation;
                }
                asset = assetRepresentation;
                objectsInHierarchy.UnionWith(EditorUtility.CollectDeepHierarchy(new[] { asset }).Select(x => x.GetInstanceID()));
                Debug.Log($"Include Asset {asset.name} ({asset.GetType().Name})");
                ctx.AddObjectToAsset(localId.ToString(), asset);
                results.Add(new Result
                {
                    asset = asset,
                    identifier = localId,
                });
            }

            using SerializedObject serializedAsset = new SerializedObject(asset);
            var currentProperty = serializedAsset.GetIterator();
            while (currentProperty.Next(true))
            {                
                if (currentProperty.propertyType != SerializedPropertyType.ObjectReference || !currentProperty.objectReferenceValue || currentProperty.objectReferenceValue is MonoScript)
                {
                    continue;
                }

                Debug.Log($"{currentProperty.depth} [{currentProperty.name}] Type: {currentProperty.type} Property Type: {currentProperty.propertyType}");

                if (!assetRepresentations.TryGetValue(currentProperty.objectReferenceInstanceIDValue, out Object propertyAsset))
                {
                    propertyAsset = CollectDependenciesRecursive(currentProperty.objectReferenceValue, ctx, objectsInHierarchy, assetRepresentations);
                }

                currentProperty.objectReferenceValue = propertyAsset;
            }
            serializedAsset.ApplyModifiedProperties();

            return asset;
        }

        public static Object GetAssetRepresentation(Object asset, long identifier, AssetImportContext ctx, out bool newRepresentation)
        {
            string name = asset.name;
            newRepresentation = true;
            switch (asset)
            {
                case Shader:
                    newRepresentation = false;
                    Shader shader = Shader.Find(name);
                    if (shader && AssetDatabase.GetAssetPath(asset) != ctx.assetPath)
                    {
                        Debug.LogWarning($"Found existing shader representation for asset {asset.name}");
                        return shader;
                    }
                    else
                    {
                        asset = Instantiate(asset);
                    }
                    break;
                case ScriptableObject:
                    asset = Instantiate(asset);
                    var tempAsset = ScriptableObject.CreateInstance(asset.GetType());
                    SetScriptReference(asset, MonoScript.FromScriptableObject(tempAsset).GetInstanceID());
                    DestroyImmediate(tempAsset);
                    break;
                case GameObject:
                    asset = Instantiate(asset);
                    GameObject prefabAsset = (GameObject)asset;
                    var componentGroups = prefabAsset.GetComponentsInChildren<MonoBehaviour>(true).GroupBy(x => x.GetType());
                    var tempPrefabAsset = new GameObject();
                    tempPrefabAsset.SetActive(false);
                    foreach (var componentGroup in componentGroups)
                    {
                        int scriptInstanceId = MonoScript.FromMonoBehaviour((MonoBehaviour)tempPrefabAsset.AddComponent(componentGroup.Key)).GetInstanceID();
                        foreach (var component in componentGroup)
                        {
                            SetScriptReference(component, scriptInstanceId);
                        }
                    }
                    DestroyImmediate(tempPrefabAsset);
                    break;
                case Texture2D texAsset:
                    static Texture2D DuplicateTexture(Texture2D srcTex)
                    {
                        RenderTexture renderTex = RenderTexture.GetTemporary(
                                    srcTex.width,
                                    srcTex.height,
                                    0,
                                    RenderTextureFormat.Default,
                                    RenderTextureReadWrite.Default);

                        RenderTexture previous = RenderTexture.active;
                        Graphics.Blit(srcTex, renderTex);
                        RenderTexture.active = renderTex;
                        Texture2D outputTex = new Texture2D(srcTex.width, srcTex.height);
                        outputTex.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                        outputTex.Apply(false, true);
                        RenderTexture.active = previous;
                        RenderTexture.ReleaseTemporary(renderTex);
                        return outputTex;
                    }
                    asset = DuplicateTexture(texAsset);
                    break;
                case Material matAsset:
                    asset = new Material(matAsset);
                    break;
                default:
                    asset = Instantiate(asset);
                    break;
            }

            Debug.LogWarning($"Created new asset representation for asset {name}");
            asset.name = name;
            asset.hideFlags = HideFlags.NotEditable;

            return asset;

            static void SetScriptReference(Object asset, int scriptInstanceId)
            {
                const string SCRIPT_PROPERTY = "m_Script";

                using SerializedObject serializedAsset = new SerializedObject(asset);
                var script = serializedAsset.FindProperty(SCRIPT_PROPERTY);
                //Debug.Log($"Script value: {script.objectReferenceInstanceIDValue}");
                script.objectReferenceInstanceIDValue = scriptInstanceId;
                serializedAsset.ApplyModifiedProperties();
                //Debug.Log($"Script value (new): {script.objectReferenceInstanceIDValue}");
            }
        }
    }
}