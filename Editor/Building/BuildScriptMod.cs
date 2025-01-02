using AssetsOfRain.Editor.VirtualAssets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.WriteTypes;
using UnityEditor.Build.Utilities;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using LogLevel = ThunderKit.Core.Pipelines.LogLevel;

namespace AssetsOfRain.Editor.Building
{
    [CreateAssetMenu(fileName = "BuildScriptMod.asset", menuName = "Addressables/Content Builders/Build Modded Content")]
    public class BuildScriptMod : BuildScriptPackedMode
    {
        private static readonly FieldInfo m_AllBundleInputDefs = typeof(BuildScriptPackedMode).GetField("m_AllBundleInputDefs", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo m_OutputAssetBundleNames = typeof(BuildScriptPackedMode).GetField("m_OutputAssetBundleNames", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo m_BundleToInternalId = typeof(BuildScriptPackedMode).GetField("m_BundleToInternalId", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo m_ResourceProviderData = typeof(BuildScriptPackedMode).GetField("m_ResourceProviderData", BindingFlags.Instance | BindingFlags.NonPublic);

        public override string Name => "Build Modded Content";

        public static Pipeline pipeline;

        protected override TResult DoBuild<TResult>(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
        {
            pipeline.Log(LogLevel.Information, "Building a mod with addressables!");

            var virtualAssets = new Dictionary<ObjectIdentifier, VirtualAddressableAssetImporter.Result>();
            var virtualAssetDependencies = new Dictionary<string, List<VirtualAddressableAssetImporter.BundleDependency>>();
            foreach (var virtualAssetPath in AssetDatabase.FindAssets($"glob:\"*.{VirtualAddressableAssetImporter.EXTENSION}\" a:assets").Select(AssetDatabase.GUIDToAssetPath))
            {
                if (AssetImporter.GetAtPath(virtualAssetPath) is not VirtualAddressableAssetImporter importer || importer.results == null)
                {
                    continue;
                }
                foreach (var virtualAsset in importer.results)
                {
                    if (ObjectIdentifier.TryGetObjectIdentifier(virtualAsset.asset, out ObjectIdentifier objectId))
                    {
                        virtualAssets[objectId] = virtualAsset;
                    }
                }
                virtualAssetDependencies[virtualAssetPath] = importer.bundleDependencies;
            }

            ReturnCode PostPacking(IBuildParameters parameters, IDependencyData dependencyData, IWriteData writeData)
            {
                if (writeData is not IBundleWriteData bundleWriteData)
                {
                    return ReturnCode.Error;
                }
                foreach (var assetBundleWriteOperation in bundleWriteData.WriteOperations.OfType<AssetBundleWriteOperation>())
                {
                    pipeline.Log(LogLevel.Information, $"Write command internal name", $"Internal Name\n{assetBundleWriteOperation.Command.internalName}");
                    foreach (var serializedObject in assetBundleWriteOperation.Command.serializeObjects)
                    {
                        if (virtualAssets.TryGetValue(serializedObject.serializationObject, out var virtualAsset))
                        {
                            serializedObject.serializationIndex = virtualAsset.identifier;
                            assetBundleWriteOperation.ReferenceMap.AddMapping(virtualAsset.internalBundleName, virtualAsset.identifier, serializedObject.serializationObject, true);
                        }
                    }
                }
                return ReturnCode.Success;
            }

            ReturnCode PostWriting(IBuildParameters parameters, IDependencyData dependencyData, IWriteData writeData, IBuildResults results)
            {
                if (writeData is not IBundleWriteData bundleWriteData)
                {
                    return ReturnCode.Error;
                }
                int originalLocationCount = aaContext.locations.Count;
                var bundleToNewBundleDependencies = new Dictionary<string, HashSet<string>>();
                var allNewBundleDependencies = new HashSet<string>();

                foreach (var assetBundleWriteOperation in bundleWriteData.WriteOperations.OfType<AssetBundleWriteOperation>())
                {
                    HashSet<string> newBundleDependencies = new HashSet<string>();

                    var bundleDependencies = assetBundleWriteOperation.Info.bundleAssets
                        .SelectMany(x => x.referencedObjects)
                        .Select(x => AssetDatabase.GUIDToAssetPath(x.guid))
                        .Distinct()
                        .Where(virtualAssetDependencies.ContainsKey)
                        .SelectMany(x => virtualAssetDependencies[x]);

                    foreach (var bundleDependency in bundleDependencies)
                    {
                        if (newBundleDependencies.Add(bundleDependency.primaryKey) && allNewBundleDependencies.Add(bundleDependency.primaryKey))
                        {
                            aaContext.locations.Add(new ContentCatalogDataEntry(
                                type: typeof(IAssetBundleResource),
                                internalId: bundleDependency.internalId,
                                provider: bundleDependency.providerId,
                                keys: new[] { bundleDependency.primaryKey },
                                extraData: bundleDependency.data));
                        }
                    }
                    if (newBundleDependencies.Count > 0)
                    {
                        bundleToNewBundleDependencies.Add(assetBundleWriteOperation.Info.bundleName, newBundleDependencies);
                    }
                }
                for (int i = 0; i < originalLocationCount; i++)
                {
                    ContentCatalogDataEntry location = aaContext.locations[i];
                    if (typeof(IAssetBundleResource).IsAssignableFrom(location.ResourceType) || location.Dependencies == null)
                    {
                        continue;
                    }
                    location.Dependencies.AddRange(location.Dependencies
                        .OfType<string>()
                        .Where(bundleToNewBundleDependencies.ContainsKey)
                        .SelectMany(x => bundleToNewBundleDependencies[x])
                        .Distinct());
                    /*HashSet<string> newLocationDependencies = new HashSet<string>();
                    foreach (var dependency in location.Dependencies.OfType<string>())
                    {
                        if (bundleToNewBundleDependencies.TryGetValue(dependency, out var newBundleDependencies))
                        {
                            newLocationDependencies.UnionWith(newBundleDependencies);
                        }
                    }*/
                }
#if false
                Dictionary<string, AssetLoadInfo> assetInfoByAddress = dependencyData.AssetInfo.Values.ToDictionary(x => x.address);
                for (int i = aaContext.locations.Count - 1; i >= 0; i--)
                {
                    ContentCatalogDataEntry location = aaContext.locations[i];
                    if (typeof(IAssetBundleResource).IsAssignableFrom(location.ResourceType))
                    {
                        continue;
                    }
                    if (!assetInfoByAddress.TryGetValue(location.InternalId, out var assetInfo) || assetInfo.referencedObjects == null || assetInfo.referencedObjects.Count == 0)
                    {
                        continue;
                    }
                    HashSet<string> newDependencyKeys = new HashSet<string>();
                    foreach (var dependencyPath in assetInfo.referencedObjects.Select(x => AssetDatabase.GUIDToAssetPath(x.guid)).Distinct())
                    {
                        if (!virtualAssetDependencies.TryGetValue(dependencyPath, out var bundleDependencies))
                        {
                            continue;
                        }
                        foreach (var bundleDependency in bundleDependencies)
                        {
                            if (!newDependencyKeys.Add(bundleDependency.primaryKey) || !allNewBundleDependencies.Add(bundleDependency.primaryKey))
                            {
                                continue;
                            }
                            aaContext.locations.Add(new ContentCatalogDataEntry(
                                type: typeof(IAssetBundleResource),
                                internalId: bundleDependency.internalId,
                                provider: bundleDependency.providerId,
                                keys: new[] { bundleDependency.primaryKey },
                                extraData: bundleDependency.data));
                        }
                    }
                    location.Dependencies.AddRange(newDependencyKeys);
                }
#endif
                /*foreach (var assetBundleWriteOperation in bundleWriteData.WriteOperations.OfType<AssetBundleWriteOperation>())
                {
                    foreach (var bundleAsset in assetBundleWriteOperation.Info.bundleAssets)
                    {
                        Debug.Log($"Bundle Asset: {}");
                        bundleAsset.referencedObjects;
                    }
                }*/
                /*Dictionary<string, string> bundleToInternalId = (Dictionary<string, string>)m_BundleToInternalId.GetValue(this);

                var virtualGroups = aaContext.Settings.groups.OfType<VirtualAddressableAssetGroup>();
                var virtualGroupByBundleKey = virtualGroups.ToDictionary(x => x.bundleName);

                HashSet<string> virtualDependencyEntryKeys = new HashSet<string>(virtualGroups.Select(x => x.location.primaryKey));

                for (int i = aaContext.locations.Count - 1; i >= 0; i--)
                {
                    ContentCatalogDataEntry dataEntry = aaContext.locations[i];
                    if (typeof(IAssetBundleResource).IsAssignableFrom(dataEntry.ResourceType))
                    {
                        if (dataEntry.Keys == null || dataEntry.Keys.FirstOrDefault() is not string bundleKey || !virtualGroupByBundleKey.TryGetValue(bundleKey, out var virtualGroup))
                        {
                            continue;
                        }
                        pipeline.Log(LogLevel.Information, $"Virtual Group entry: {dataEntry.InternalId}", $"Key\n{dataEntry.Keys[0]}");

                        dataEntry.Keys.Clear();
                        dataEntry.Keys.Add(virtualGroup.location.primaryKey);

                        dataEntry.InternalId = virtualGroup.location.internalId;
                        bundleToInternalId.Add(bundleKey, dataEntry.InternalId);

                        dataEntry.Data = virtualGroup.location.data;

                        foreach (var dependency in virtualGroup.dependencies)
                        {
                            if (virtualDependencyEntryKeys.Add(dependency.primaryKey))
                            {
                                aaContext.locations.Add(new ContentCatalogDataEntry(
                                    typeof(IAssetBundleResource),
                                    dependency.internalId,
                                    dataEntry.Provider,
                                    new[] { dependency.primaryKey },
                                    extraData: dependency.data));
                            }
                        }
                    }
                    else
                    {
                        if (dataEntry.Dependencies == null || dataEntry.Dependencies.Count == 0)
                        {
                            continue;
                        }
                        HashSet<string> newDependencyKeys = new HashSet<string>();
                        for (int j = 0; j < dataEntry.Dependencies.Count; j++)
                        {
                            if (dataEntry.Dependencies[j] is string dependencyKey && virtualGroupByBundleKey.TryGetValue(dependencyKey, out var virtualGroup))
                            {
                                dataEntry.Dependencies[j] = virtualGroup.location.primaryKey;
                                newDependencyKeys.UnionWith(virtualGroup.dependencies.Select(x => x.primaryKey));
                            }
                        }
                        dataEntry.Dependencies.AddRange(newDependencyKeys);
                    }
                }*/
                return ReturnCode.Success;
            }

            ContentPipeline.BuildCallbacks.PostPackingCallback = PostPacking;
            ContentPipeline.BuildCallbacks.PostWritingCallback = PostWriting;
            pipeline.Log(LogLevel.Information, "Continuing build..");
            var buildResult = base.DoBuild<TResult>(builderInput, aaContext);
            ContentPipeline.BuildCallbacks.PostPackingCallback = null;
            ContentPipeline.BuildCallbacks.PostWritingCallback = null;
            return buildResult;
        }

#if false
        protected override TResult DoBuild<TResult>(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
        {
            pipeline.Log(LogLevel.Information, "Building a mod with addressables!");

            List<AssetBundleBuild> allBundleInputDefs = (List<AssetBundleBuild>)m_AllBundleInputDefs.GetValue(this);
            if (allBundleInputDefs != null && allBundleInputDefs.Count > 0)
            {
                foreach (var bundleToAssetGroupPair in aaContext.bundleToAssetGroup.ToArray())
                {
                    if (aaContext.Settings.FindGroup(x => x.Guid == bundleToAssetGroupPair.Value) is not VirtualAddressableAssetGroup group)
                    {
                        continue;
                    }

                    aaContext.bundleToAssetGroup.Remove(bundleToAssetGroupPair.Key);
                    aaContext.bundleToAssetGroup.Add(group.bundleName, bundleToAssetGroupPair.Value);

                    for (int i = 0; i < allBundleInputDefs.Count; i++)
                    {
                        var bundleInputDef = allBundleInputDefs[i];
                        if (bundleInputDef.assetBundleName == bundleToAssetGroupPair.Key)
                        {
                            bundleInputDef.assetBundleName = group.bundleName;
                            allBundleInputDefs[i] = bundleInputDef;
                        }
                    }
                }
            }

            ReturnCode PostWriting(IBuildParameters parameters, IDependencyData dependencyData, IWriteData writeData, IBuildResults results)
            {
                Dictionary<string, string> bundleToInternalId = (Dictionary<string, string>)m_BundleToInternalId.GetValue(this);

                var virtualGroups = aaContext.Settings.groups.OfType<VirtualAddressableAssetGroup>();
                var virtualGroupByBundleKey = virtualGroups.ToDictionary(x => x.bundleName);

                HashSet<string> virtualDependencyEntryKeys = new HashSet<string>(virtualGroups.Select(x => x.location.primaryKey));

                for (int i = aaContext.locations.Count - 1; i >= 0; i--)
                {
                    ContentCatalogDataEntry dataEntry = aaContext.locations[i];
                    if (typeof(IAssetBundleResource).IsAssignableFrom(dataEntry.ResourceType))
                    {
                        if (dataEntry.Keys == null || dataEntry.Keys.FirstOrDefault() is not string bundleKey || !virtualGroupByBundleKey.TryGetValue(bundleKey, out var virtualGroup))
                        {
                            continue;
                        }
                        pipeline.Log(LogLevel.Information, $"Virtual Group entry: {dataEntry.InternalId}", $"Key\n{dataEntry.Keys[0]}");

                        dataEntry.Keys.Clear();
                        dataEntry.Keys.Add(virtualGroup.location.primaryKey);

                        dataEntry.InternalId = virtualGroup.location.internalId;
                        bundleToInternalId.Add(bundleKey, dataEntry.InternalId);

                        dataEntry.Data = virtualGroup.location.data;

                        foreach (var dependency in virtualGroup.dependencies)
                        {
                            if (virtualDependencyEntryKeys.Add(dependency.primaryKey))
                            {
                                aaContext.locations.Add(new ContentCatalogDataEntry(
                                    typeof(IAssetBundleResource),
                                    dependency.internalId,
                                    dataEntry.Provider,
                                    new[] { dependency.primaryKey },
                                    extraData: dependency.data));
                            }
                        }
                    }
                    else
                    {
                        if (dataEntry.Dependencies == null || dataEntry.Dependencies.Count == 0)
                        {
                            continue;
                        }
                        HashSet<string> newDependencyKeys = new HashSet<string>();
                        for (int j = 0; j < dataEntry.Dependencies.Count; j++)
                        {
                            if (dataEntry.Dependencies[j] is string dependencyKey && virtualGroupByBundleKey.TryGetValue(dependencyKey, out var virtualGroup))
                            {
                                dataEntry.Dependencies[j] = virtualGroup.location.primaryKey;
                                newDependencyKeys.UnionWith(virtualGroup.dependencies.Select(x => x.primaryKey));
                            }
                        }
                        dataEntry.Dependencies.AddRange(newDependencyKeys);
                    }
                }
                return ReturnCode.Success;
            }

            ReturnCode PostPacking(IBuildParameters parameters, IDependencyData dependencyData, IWriteData writeData)
            {
                if (writeData is IBundleWriteData bundleWriteData)
                {
                    Dictionary<ObjectIdentifier, long> virtualAssetIdentifiers = new Dictionary<ObjectIdentifier, long>();
                    foreach (var virtualAssetPath in AssetDatabase.FindAssets($"glob:\"*.{VirtualAddressableAssetImporter.EXTENSION}\" a:assets").Select(AssetDatabase.GUIDToAssetPath))
                    {
                        if (AssetImporter.GetAtPath(virtualAssetPath) is not VirtualAddressableAssetImporter importer || importer.results == null)
                        {
                            continue;
                        }
                        foreach (var result in importer.results)
                        {
                            if (ObjectIdentifier.TryGetObjectIdentifier(result.asset, out ObjectIdentifier objectId))
                            {
                                virtualAssetIdentifiers[objectId] = result.identifier;
                            }
                        }
                    }
                    foreach (var writeOperation in writeData.WriteOperations)
                    {
                        foreach (var serializeObject in writeOperation.Command.serializeObjects)
                        {
                            if (virtualAssetIdentifiers.TryGetValue(serializeObject.serializationObject, out long identifier))
                            {
                                serializeObject.serializationIndex = identifier;
                                writeOperation.ReferenceMap.AddMapping(writeOperation.Command.internalName, identifier, serializeObject.serializationObject, true);
                            }
                        }
                    }
                    // BuildReferenceMap details what objects exist in other bundles that objects in a source bundle depend upon (forward dependencies)
                    // BuildUsageTagSet details the conditional data needed to be written by objects in a source bundle that is in used by objects in other bundles (reverse dependencies)

                    static void GetOrAdd<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key, out TValue value) where TValue : new()
                    {
                        if (dictionary.TryGetValue(key, out value))
                            return;

                        value = new TValue();
                        dictionary.Add(key, value);
                    }

                    var fileToCommand = bundleWriteData.WriteOperations.ToDictionary(x => x.Command.internalName, x => x.Command);
                    var forwardObjectDependencies = new Dictionary<string, HashSet<ObjectIdentifier>>();
                    var forwardFileDependencies = new Dictionary<string, HashSet<string>>();
                    foreach (var pair in bundleWriteData.AssetToFiles)
                    {
                        GUID asset = pair.Key;
                        List<string> files = pair.Value;

                        // The includes for an asset live in the first file, references could live in any file
                        GetOrAdd(forwardObjectDependencies, files[0], out HashSet<ObjectIdentifier> objectDependencies);
                        GetOrAdd(forwardFileDependencies, files[0], out HashSet<string> fileDependencies);

                        // Grab the list of object references for the asset or scene and add them to the forward dependencies hash set for this file (write command)
                        if (dependencyData.AssetInfo.TryGetValue(asset, out AssetLoadInfo assetInfo))
                            objectDependencies.UnionWith(assetInfo.referencedObjects);
                        if (dependencyData.SceneInfo.TryGetValue(asset, out SceneDependencyInfo sceneInfo))
                            objectDependencies.UnionWith(sceneInfo.referencedObjects);

                        // Grab the list of file references for the asset or scene and add them to the forward dependencies hash set for this file (write command)
                        // While doing so, also add the asset to the reverse dependencies hash set for all the other files it depends upon.
                        // We already ensure BuildReferenceMap & BuildUsageTagSet contain the objects in this write command in GenerateBundleCommands. So skip over the first file (self)
                        for (int i = 1; i < files.Count; i++)
                        {
                            fileDependencies.Add(files[i]);
                        }
                    }


                    // Using the previously generated forward dependency maps, update the BuildReferenceMap per WriteCommand to contain just the references that we care about

                    foreach (var operation in bundleWriteData.WriteOperations)
                    {
                        var internalName = operation.Command.internalName;

                        BuildReferenceMap referenceMap = bundleWriteData.FileToReferenceMap[internalName];
                        if (!forwardObjectDependencies.TryGetValue(internalName, out var objectDependencies))
                            continue; // this bundle has no external dependencies
                        if (!forwardFileDependencies.TryGetValue(internalName, out var fileDependencies))
                            continue; // this bundle has no external dependencies
                        foreach (string file in fileDependencies)
                        {
                            WriteCommand dependentCommand = fileToCommand[file];
                            foreach (var serializeObject in dependentCommand.serializeObjects)
                            {
                                // Only add objects we are referencing. This ensures that new/removed objects to files we depend upon will not cause a rebuild
                                // of this file, unless are referencing the new/removed objects.
                                if (!objectDependencies.Contains(serializeObject.serializationObject))
                                    continue;

                                if (virtualAssetIdentifiers.TryGetValue(serializeObject.serializationObject, out long identifier))
                                {
                                    referenceMap.AddMapping(file, identifier, serializeObject.serializationObject, true);
                                }
                            }
                        }
                    }
                    foreach (var virtualAsset in virtualAssetIdentifiers.Keys)
                    {
                        bundleWriteData.AssetToFiles.Remove(virtualAsset.guid);
                    }
                    foreach (var fileToObjects in bundleWriteData.FileToObjects)
                    {
                        pipeline.Log(LogLevel.Information, $"Removed {fileToObjects.Value.RemoveAll(virtualAssetIdentifiers.ContainsKey)} assets");
                    }
                    foreach (var writeOperation in bundleWriteData.WriteOperations)
                    {
                        if (aaContext.Settings.groups.OfType<VirtualAddressableAssetGroup>().Any(x => x.bundleName == writeOperation.Command.fileName))
                        {
                        }
                        if (writeOperation is AssetBundleWriteOperation assetBundleWriteOperation)
                        {
                            if (assetBundleWriteOperation.Info.bundleAssets.RemoveAll(x => x.includedObjects.Any(virtualAssetIdentifiers.ContainsKey)) > 0)
                            {
                                assetBundleWriteOperation.UsageSet = new BuildUsageTagSet();
                                assetBundleWriteOperation.ReferenceMap = new BuildReferenceMap();
                            }
                        }
                        writeOperation.Command.serializeObjects.RemoveAll(x => virtualAssetIdentifiers.ContainsKey(x.serializationObject));
                    }
                    /*bundleWriteData.FileToReferenceMap.Clear();
                    bundleWriteData.FileToObjects.Clear();
                    bundleWriteData.FileToBundle.Clear();
                    bundleWriteData.AssetToFiles.Clear();
                    bundleWriteData.FileToUsageSet.Clear();
                    bundleWriteData.WriteOperations.Clear();*/
                }
                return ReturnCode.Success;
            }

            ContentPipeline.BuildCallbacks.PostWritingCallback = PostWriting;
            ContentPipeline.BuildCallbacks.PostPackingCallback = PostPacking;
            pipeline.Log(LogLevel.Information, "Continuing build..");
            var buildResult = base.DoBuild<TResult>(builderInput, aaContext);
            ContentPipeline.BuildCallbacks.PostWritingCallback = null;
            ContentPipeline.BuildCallbacks.PostPackingCallback = null;
            return buildResult;
        }
#endif
    }
}