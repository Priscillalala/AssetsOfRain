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
    // The secret sauce
    // Generated .virtualaa files are empty. This importer stores serialized info like the request and results in
    // the corresponding .virtualaa.meta file, and the virtual assets are stored somewhere in Unity's temp files
    [ScriptedImporter(0, EXTENSION_NO_PERIOD)]
    public class VirtualAddressableAssetImporter : ScriptedImporter
    {
        // Used to map virtual assets to runtime assets
        [Serializable]
        public struct Result
        {
            public Object asset;
            public long identifier;
            public string internalBundleName;
        }

        // Represents an existing assetbundle resource location
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

        public const string EXTENSION_NO_PERIOD = "virtualaa";
        public const string EXTENSION = "." + EXTENSION_NO_PERIOD;
        const int MAX_SCRIPTABLEOBJECT_DEPTH = 1;
        const int MAX_PREFAB_DEPTH = 1;

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

            asset = CollectAssetsRecursive(asset, ctx, -1, new HashSet<int>(), new Dictionary<int, Object>(), assetToBundleMap);
            if (results.Count > 0)
            {
                asset.hideFlags = HideFlags.NotEditable;
                ctx.SetMainObject(asset);
            }
            else
            {
                Debug.LogWarning($"Virtual asset importer {ctx.assetPath} generated no assets");
            }
        }

        public Object CollectAssetsRecursive(Object asset, AssetImportContext ctx, int depth, HashSet<int> objectsInHierarchy, Dictionary<int, Object> assetRepresentations, Dictionary<long, string> assetToBundleMap)
        {
            int assetInstanceId = asset.GetInstanceID();
            if (objectsInHierarchy.Contains(assetInstanceId))
            {
                // This is a component or child gameobject whos parent has already been instantiated, it doesn't need a new representation
                assetRepresentations[assetInstanceId] = asset;
            }
            else
            {
                // The localId of loaded addressable assets matches their pathId in the assetbundle
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long localId);
                if (!assetToBundleMap.ContainsKey(localId))
                {
                    Debug.LogWarning($"Virtual asset importer {ctx.assetPath} found asset {(asset != null ? asset.name : "null")} with id {localId} which did not map to any bundle");
                    return null;
                }
                Object assetRepresentation = GetAssetRepresentation(asset, ctx, ++depth, out bool newRepresentation);
                assetRepresentations[assetInstanceId] = assetRepresentation;
                if (!assetRepresentation)
                {
                    // Probably culled on purpose
                    return null;
                }
                assetRepresentations[assetRepresentation.GetInstanceID()] = assetRepresentation;
                if (!newRepresentation)
                {
                    return assetRepresentation;
                }
                asset = assetRepresentation;
                objectsInHierarchy.UnionWith(EditorUtility.CollectDeepHierarchy(new[] { asset }).Select(x => x.GetInstanceID()));
                ctx.AddObjectToAsset(localId.ToString(), asset);
                results.Add(new Result
                {
                    asset = asset,
                    identifier = localId,
                    internalBundleName = assetToBundleMap[localId]
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

                if (!assetRepresentations.TryGetValue(currentProperty.objectReferenceInstanceIDValue, out Object propertyAsset))
                {
                    propertyAsset = CollectAssetsRecursive(currentProperty.objectReferenceValue, ctx, depth, objectsInHierarchy, assetRepresentations, assetToBundleMap);
                }

                currentProperty.objectReferenceValue = propertyAsset;
            }
            serializedAsset.ApplyModifiedProperties();

            return asset;
        }

        // Get a mutable instance of the loaded asset that will play nice in the project
        // For shaders, attempts to return an existing virtual shader asset already in the project
        public static Object GetAssetRepresentation(Object asset, AssetImportContext ctx, int depth, out bool newRepresentation)
        {
            string name = asset.name;
            newRepresentation = true;
            switch (asset)
            {
                case Shader shaderAsset:
                    Shader existingShader = Shader.Find(name);
                    string existingShaderPath = AssetDatabase.GetAssetPath(existingShader);
                    if (existingShader && !string.IsNullOrEmpty(existingShaderPath) && existingShaderPath != ctx.assetPath)
                    {
                        if (existingShaderPath.StartsWith("Assets"))
                        {
                            newRepresentation = false;
                            return existingShader;
                        }
                        else
                        {
                            // This is a builtin shader, we don't want to directly reference it because addressables
                            // will generate the builtin shaders bundle
                            asset = Instantiate(existingShader);
                            asset.hideFlags |= HideFlags.HideInHierarchy;
                            using SerializedObject serializedClonedAsset = new SerializedObject(asset);

                            // builtin shaders can depend on other builtin shaders and this is the easiest way to stop that
                            serializedClonedAsset.FindProperty("m_ParsedForm.m_FallbackName").stringValue = string.Empty;
                            serializedClonedAsset.FindProperty("m_Dependencies").ClearArray();
                            serializedClonedAsset.ApplyModifiedProperties();
                        }
                    }
                    else
                    {
                        asset = Instantiate(shaderAsset);
                        if (!((Shader)asset).isSupported)
                        {
                            // If the shader is unsupported we replace it with a supported dummy shader to move
                            // it out of the "Not Supported" tab
                            DestroyImmediate(asset);
                            asset = ImportUtil.GetDummyShader(shaderAsset, ctx);
                        }
                    }
                    break;
                case ScriptableObject scriptableAsset:
                    if (depth > MAX_SCRIPTABLEOBJECT_DEPTH)
                    {
                        // Scriptable objects can create massive dependency trees
                        return null;
                    }
                    asset = ScriptableObject.CreateInstance(scriptableAsset.GetType());
                    EditorUtility.CopySerialized(scriptableAsset, asset);
                    break;
                case GameObject:
                    if (depth > MAX_PREFAB_DEPTH)
                    {
                        // Prefabs can create unholy dependency trees
                        return null;
                    }
                    asset = Instantiate(asset);
                    // Prevents this instantiation from dirtying the current scene
                    asset.hideFlags |= HideFlags.DontSave;
                    GameObject prefabAsset = (GameObject)asset;
                    var componentGroups = prefabAsset.GetComponentsInChildren<MonoBehaviour>(true).GroupBy(x => x.GetType());
                    foreach (var componentGroup in componentGroups)
                    {
                        MonoScript monoScript = ImportUtil.FindScript(componentGroup.Key);
                        foreach (var component in componentGroup)
                        {
                            ImportUtil.SetScriptReference(component, monoScript);
                        }
                    }
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

            asset.name = name;
            asset.hideFlags = HideFlags.NotEditable | HideFlags.HideInHierarchy;

            return asset;
        }

        // The addressable asset dependency lists are innaccurate, so we narrow it down to the genuine dependencies of the asset's assetbundle
        // We also use the assetbundle's preload table to map every asset we might encounter during asset collection to an asset bundle
        public void PopulateBundleDependencies(IResourceLocation bundleLocation, IList<IResourceLocation> possibleDependencies, out Dictionary<long, string> assetToBundleMap)
        {
            const string ASSETBUNDLE_PRELOADTABLE_PROPERTY = "m_PreloadTable";
            const string ASSETBUNDLE_DEPENDENCIES_PROPERTY = "m_Dependencies";
            const string ARRAY_PROPERTY = "Array";
            const string FILE_ID_PROPERTY = "m_FileID";
            const string PATH_ID_PROPERTY = "m_PathID";

            assetToBundleMap = new Dictionary<long, string>();

            AssetsManager assetsManager = new AssetsManager();

            var bundleFile = assetsManager.LoadBundleFile(Addressables.ResourceManager.TransformInternalId(bundleLocation));
            var assetFile = assetsManager.LoadAssetsFileFromBundle(bundleFile, 0, false);

            string internalBundleName = string.Format(CommonStrings.AssetBundleNameFormat, assetFile.name);
            // externals map FileIDs to asset bundles
            var externals = assetFile.file.Metadata.Externals;
            var assetBundleAsset = assetFile.file.GetAssetsOfType((int)AssetClassID.AssetBundle)[0];
            var assetBundle = assetsManager.GetBaseField(assetFile, assetBundleAsset);

            var preloadTableField = assetBundle[ASSETBUNDLE_PRELOADTABLE_PROPERTY][ARRAY_PROPERTY];
            foreach (var data in preloadTableField)
            {
                int fileID = data[FILE_ID_PROPERTY].AsInt;
                long pathID = data[PATH_ID_PROPERTY].AsLong;
                assetToBundleMap[pathID] = fileID == 0 ? internalBundleName : externals[fileID - 1].OriginalPathName;
            }

            var dependenciesField = assetBundle[ASSETBUNDLE_DEPENDENCIES_PROPERTY][ARRAY_PROPERTY];
            HashSet<string> internalDependencyNames = new HashSet<string>(dependenciesField.Select(x => x.AsString));

            assetsManager.UnloadAll(true);

            bundleDependencies.Add(new BundleDependency(bundleLocation));

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
                // Reimplementing the standard IDeterministicIdentifiers.GenerateInternalFileName implementation to
                // run faster in large batches
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