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

        public SerializableAssetRequest request;

        [Serializable]
        public struct Result
        {
            public Object asset;
            public long identifier;
        }

        public List<Result> results = new List<Result>();

        public override void OnImportAsset(AssetImportContext ctx)
        {
            IResourceLocation assetLocation = request.AssetLocation;
            if (assetLocation == null)
            {
                return;
            }
            AsyncOperationHandle asyncOp = Addressables.ResourceManager.ProvideResource(assetLocation, request.AssetType, true);
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
                Object assetRepresentation = GetAssetRepresentation(asset, ctx, out bool newRepresentation, out bool recurseDependencies);
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
                if (!recurseDependencies)
                {
                    return asset;
                }
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

        public static Object GetAssetRepresentation(Object asset, AssetImportContext ctx, out bool newRepresentation, out bool recurseDependencies)
        {
            string name = asset.name;
            newRepresentation = true;
            recurseDependencies = true;
            switch (asset)
            {
                case Shader:
                    Shader shader = Shader.Find(name);
                    if (shader && AssetDatabase.GetAssetPath(shader) != ctx.assetPath)
                    {
                        newRepresentation = false;
                        Debug.LogWarning($"Found existing shader representation for asset {asset.name}");
                        return shader;
                    }
                    else
                    {
                        //recurseDependencies = false;
                        asset = Instantiate(asset);
                    }
                    break;
                case ScriptableObject scriptableAsset:
                    asset = ScriptableObject.CreateInstance(scriptableAsset.GetType());
                    EditorUtility.CopySerialized(scriptableAsset, asset);
                    break;
                case GameObject:
                    asset = Instantiate(asset);
                    asset.hideFlags |= HideFlags.DontSave;
                    GameObject prefabAsset = (GameObject)asset;
                    var componentGroups = prefabAsset.GetComponentsInChildren<MonoBehaviour>(true).GroupBy(x => x.GetType());
                    var tempPrefabAsset = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
                    tempPrefabAsset.SetActive(false);
                    foreach (var componentGroup in componentGroups)
                    {
                        MonoScript monoScript = MonoScript.FromMonoBehaviour((MonoBehaviour)tempPrefabAsset.AddComponent(componentGroup.Key));
                        foreach (var component in componentGroup)
                        {
                            ImportUtil.SetScriptReference(component, monoScript);
                        }
                    }
                    DestroyImmediate(tempPrefabAsset);
                    break;
                case Texture2D texAsset:
                    asset = ImportUtil.DuplicateCompressedTexture(texAsset);
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
            asset.hideFlags = HideFlags.NotEditable | HideFlags.HideInHierarchy;

            return asset;
        }
    }
}