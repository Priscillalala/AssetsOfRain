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
using AssetsOfRain.Editor.Util;
using System.IO;
using UnityEngine.ResourceManagement.ResourceProviders;
using System.Text;
using System.Xml.Linq;
using UnityMD4 = UnityEditor.Build.Pipeline.Utilities.MD4;
using UnityEditor.Build.Pipeline.Utilities;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Utilities;

namespace AssetsOfRain.Editor.VirtualAssets
{
    [ScriptedImporter(0, EXTENSION)]
    public class VirtualAddressableAssetImporter : ScriptedImporter
    {
        [Serializable]
        public struct Result
        {
            public Object asset;
            public long identifier;
            public string internalBundleName;
        }

        [Serializable]
        public struct BundleDependency
        {
            public string primaryKey;
            public string internalId;
            public string providerId;
            public AssetBundleRequestOptions data;

            public BundleDependency(IResourceLocation bundleLocation)
            {
                primaryKey = bundleLocation.PrimaryKey;
                internalId = Path.Combine("{UnityEngine.AddressableAssets.Addressables.RuntimePath}", "StandaloneWindows64", Path.GetFileName(bundleLocation.InternalId));
                providerId = bundleLocation.ProviderId;
                data = (AssetBundleRequestOptions)bundleLocation.Data;
            }
        }

        public const string EXTENSION = "virtualaa";

        public SerializableAssetRequest request;

        public List<Result> results = new List<Result>();
        public List<BundleDependency> bundleDependencies = new List<BundleDependency>();

        public override void OnImportAsset(AssetImportContext ctx)
        {
            results.Clear();
            bundleDependencies.Clear();

            IResourceLocation assetLocation = request.AssetLocation;
            if (assetLocation == null)
            {
                return;
            }
            IResourceLocation bundleLocation = assetLocation.Dependencies.FirstOrDefault(x => x.Data is AssetBundleRequestOptions);
            if (bundleLocation == null)
            {
                Debug.LogWarning($"Tried to import asset {request.primaryKey} but could not find an asset bundle");
                return;
            }
            PopulateBundleDependencies(bundleLocation, assetLocation.Dependencies, out var assetToBundleMap);

            AsyncOperationHandle asyncOp = Addressables.ResourceManager.ProvideResource(assetLocation, request.AssetType, true);
            Object asset = (Object)asyncOp.WaitForCompletion();
            if (!asset)
            {
                Debug.LogWarning($"Tried to import asset {request.primaryKey} which does not exist");
                return;
            }

            asset = CollectDependenciesRecursive(asset, ctx, new HashSet<int>(), new Dictionary<int, Object>(), assetToBundleMap);
            asset.hideFlags = HideFlags.NotEditable;
            ctx.SetMainObject(asset);
        }

        public Object CollectDependenciesRecursive(Object asset, AssetImportContext ctx, HashSet<int> objectsInHierarchy, Dictionary<int, Object> assetRepresentations, Dictionary<long, string> assetToBundleMap)
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
                    internalBundleName = assetToBundleMap[localId]
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

                //Debug.Log($"{currentProperty.depth} [{currentProperty.name}] Type: {currentProperty.type} Property Type: {currentProperty.propertyType}");

                if (!assetRepresentations.TryGetValue(currentProperty.objectReferenceInstanceIDValue, out Object propertyAsset))
                {
                    propertyAsset = CollectDependenciesRecursive(currentProperty.objectReferenceValue, ctx, objectsInHierarchy, assetRepresentations, assetToBundleMap);
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

        public void PopulateBundleDependencies(IResourceLocation bundleLocation, IList<IResourceLocation> possibleDependencies, out Dictionary<long, string> assetToBundleMap)
        {
            assetToBundleMap = new Dictionary<long, string>();

            AssetsManager assetsManager = new AssetsManager();

            var bundleFile = assetsManager.LoadBundleFile(Addressables.ResourceManager.TransformInternalId(bundleLocation));
            var assetFile = assetsManager.LoadAssetsFileFromBundle(bundleFile, 0, false);
            string internalBundleName = string.Format(CommonStrings.AssetBundleNameFormat, assetFile.name);
            Debug.Log($"internalBundleName: {internalBundleName}");
            var externals = assetFile.file.Metadata.Externals;
            var assetBundleAsset = assetFile.file.GetAssetsOfType((int)AssetClassID.AssetBundle)[0];
            var assetBundle = assetsManager.GetBaseField(assetFile, assetBundleAsset);
            //Debug.Log($"internalBundleName from md5: {new Unity5PackedIdentifiers().GenerateInternalFileName(assetBundle["m_AssetBundleName"].AsString)}");
            //string internalBundleName = new Unity5PackedIdentifiers().GenerateInternalFileName(assetBundle["m_AssetBundleName"].AsString);

            var dependenciesField = assetBundle["m_Dependencies"]["Array"];
            string[] dependencies = dependenciesField.Select(x => x.AsString).ToArray();

            var preloadTableField = assetBundle["m_PreloadTable"]["Array"];
            foreach (var data in preloadTableField)
            {
                int fileID = data["m_FileID"].AsInt;
                long pathID = data["m_PathID"].AsLong;
                Debug.Log($"fileId: {fileID}, pathId: {pathID}");
                assetToBundleMap[pathID] = fileID == 0 ? internalBundleName : externals[fileID - 1].OriginalPathName;
            }

            assetsManager.UnloadAll(true);

            bundleDependencies.Add(new BundleDependency(bundleLocation));

            HashSet<string> internalDependencyNames = new HashSet<string>(dependencies);
            if (internalDependencyNames.Count == 0)
            {
                return;
            }
            using UnityMD4 hashingAlgorithm = UnityMD4.Create();
            foreach (IResourceLocation possibleDependency in possibleDependencies)
            {
                if (possibleDependency.Data is not AssetBundleRequestOptions assetBundleRequestOptions)
                {
                    continue;
                }
                string dependencyBundleName = Path.ChangeExtension(assetBundleRequestOptions.BundleName, "bundle");
                byte[] dependencyBundleNameHash = hashingAlgorithm.ComputeHash(Encoding.ASCII.GetBytes(dependencyBundleName));
                string internalName = "cab-" + BitConverter.ToString(dependencyBundleNameHash).Replace("-", "").ToLower();
                if (internalDependencyNames.Remove(internalName))
                {
                    bundleDependencies.Add(new BundleDependency(possibleDependency));
                    if (internalDependencyNames.Count == 0)
                    {
                        break;
                    }
                }
            }
            if (internalDependencyNames.Count > 0)
            {
                Debug.LogWarning($"Asset {request.primaryKey} failed to locate the following bundle dependencies: {string.Join(", ", internalDependencyNames)}");
            }
        }
    }
}