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
            /*AssetBundle assetBundle = Addressables.LoadAssetAsync<IAssetBundleResource>(bundleLocation).WaitForCompletion().GetAssetBundle();

            PopulateBundleDependencies(bundleLocation, assetBundle, assetLocation.Dependencies);
            var assetToBundleMap = GenerateAssetToBundleMap(assetBundle);*/

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
            Debug.Log($"Bundle file name {bundleFile.name}");
            var assetFile = assetsManager.LoadAssetsFileFromBundle(bundleFile, 0, false);
            string internalBundleName = assetFile.name;
            Debug.Log($"Assets file name {assetFile.name}");
            
            var assetBundleAsset = assetFile.file.GetAssetsOfType((int)AssetClassID.AssetBundle)[0];
            //var assetBundleExtAsset = assetsManager.GetExtAsset(assetFile, 0, assetBundleAsset.PathId);
            var assetBundle = assetsManager.GetBaseField(assetFile, assetBundleAsset);

            //string bundleName = assetBundle["m_AssetBundleName"].AsString;
            //Debug.Log($"Bundle Name: {bundleName}");
            var dependenciesField = assetBundle["m_Dependencies"]["Array"];
            string[] dependencies = dependenciesField.Select(x => x.AsString).ToArray();

            var preloadTableField = assetBundle["m_PreloadTable"]["Array"];
            foreach (var data in preloadTableField)
            {
                int fileID = data["m_FileID"].AsInt;
                long pathID = data["m_PathID"].AsLong;
                Debug.Log($"fileId: {fileID}, pathId: {pathID}");
                assetToBundleMap[pathID] = fileID == 0 ? internalBundleName : dependencies[fileID - 1];
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

#if false
        /*public List<IResourceLocation> ReduceBundleDependencyLocations(IResourceLocation mainBundleDependency, IEnumerable<IResourceLocation> possibleDependencies)
        {
            List<IResourceLocation> reducedBundleDependencies = new List<IResourceLocation>();
            if (mainBundleDependency == null)
            {
                return reducedBundleDependencies;
            }
            reducedBundleDependencies.Add(mainBundleDependency);

            var assetBundleResource = Addressables.LoadAssetAsync<IAssetBundleResource>(mainBundleDependency).WaitForCompletion();
            using SerializedObject serializedObject = new SerializedObject(assetBundleResource.GetAssetBundle());
            var m_Dependencies = serializedObject.FindProperty("m_Dependencies");
            HashSet<string> internalDependencyNames = new HashSet<string>();

            for (int i = 0; i < m_Dependencies.arraySize; i++)
            {
                string internalDependencyName = m_Dependencies.GetArrayElementAtIndex(i).stringValue;
                internalDependencyNames.Add(internalDependencyName);
            }
            if (internalDependencyNames.Count == 0)
            {
                return reducedBundleDependencies;
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
                    reducedBundleDependencies.Add(possibleDependency);
                    if (internalDependencyNames.Count == 0)
                    {
                        break;
                    }
                }
            }
            if (internalDependencyNames.Count > 0)
            {
                Debug.LogWarning($"Asset {request.primaryKey} failed to locate the following bundle dependencies: {string.Join(", ", internalDependencyNames)} for bundle {mainBundleDependency.PrimaryKey}");
            }
            return reducedBundleDependencies;
        }*/

        public Dictionary<long, string> GenerateAssetToBundleMap(AssetBundle assetBundle)
        {
            Dictionary<long, string> assetToBundleMap = new Dictionary<long, string>();

            if (assetBundle == null)
            {
                return assetToBundleMap;
            }

            string internalBundleName = "cab-" + HashingMethods.Calculate<UnityMD4>(assetBundle.name);
            using SerializedObject serializedAssetBundle = new SerializedObject(assetBundle);
            var m_Container = serializedAssetBundle.FindProperty("m_Container");
            for (int i = 0; i < m_Container.arraySize; i++)
            {
                var data = m_Container.GetArrayElementAtIndex(i);
                var asset = m_Container.FindPropertyRelative("second/");
            }
            /*var currentProperty = serializedAssetBundle.GetIterator();
            while (currentProperty.Next(true))
            {
                if (currentProperty.propertyType == SerializedPropertyType.String)
                {
                    Debug.Log($"{currentProperty.depth} [{currentProperty.name}] Type: {currentProperty.type} Property Type: {currentProperty.propertyType} Value: {currentProperty.stringValue}");

                }
                else
                {
                    Debug.Log($"{currentProperty.depth} [{currentProperty.name}] Type: {currentProperty.type} Property Type: {currentProperty.propertyType}");
                }
            }*/


            /*var m_PreloadTable = serializedAssetBundle.FindProperty("m_PreloadTable");
            var m_Dependencies = serializedAssetBundle.FindProperty("m_Dependencies");

            for (int i = 0; i < m_PreloadTable.arraySize; i++)
            {
                var preloadData = m_PreloadTable.GetArrayElementAtIndex(i);
                //Debug.Log($"{preloadData.depth} [{preloadData.name}] Type: {preloadData.type} Property Type: {preloadData.propertyType}");

                var m_FileID = preloadData.FindPropertyRelative("m_FileID");
                int fileId = m_FileID.intValue;

                var m_PathID = preloadData.FindPropertyRelative("m_PathID");
                long pathId = m_PathID.longValue;

                Debug.Log($"fileId: {fileId} {m_FileID.intValue} {m_FileID.longValue}, pathId: {pathId} {m_PathID.intValue} {m_PathID.longValue}");
                //assetToBundleMap.Add(pathId, fileId == 0 ? internalBundleName : m_Dependencies.GetArrayElementAtIndex(fileId - 1).stringValue);
            }*/

            return assetToBundleMap;
        }

        public void PopulateBundleDependencies(IResourceLocation bundleLocation, AssetBundle assetBundle, IList<IResourceLocation> possibleDependencies)
        {
            if (bundleLocation == null || assetBundle == null)
            {
                return;
            }
            bundleDependencies.Add(new BundleDependency(bundleLocation));

            var assetBundleResource = Addressables.LoadAssetAsync<IAssetBundleResource>(bundleLocation).WaitForCompletion();
            using SerializedObject serializedObject = new SerializedObject(assetBundleResource.GetAssetBundle());
            var m_Dependencies = serializedObject.FindProperty("m_Dependencies");
            HashSet<string> internalDependencyNames = new HashSet<string>();

            for (int i = 0; i < m_Dependencies.arraySize; i++)
            {
                string internalDependencyName = m_Dependencies.GetArrayElementAtIndex(i).stringValue;
                internalDependencyNames.Add(internalDependencyName);
            }
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
#endif
    }
}