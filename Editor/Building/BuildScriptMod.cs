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

            aaContext = new ModdedAddressableAssetsBuildContext
            {
                Settings = aaContext.Settings,
                runtimeData = aaContext.runtimeData,
                locations = aaContext.locations,
                bundleToAssetGroup = aaContext.bundleToAssetGroup,
                assetGroupToBundles = aaContext.assetGroupToBundles,
                providerTypes = aaContext.providerTypes,
                assetEntries = aaContext.assetEntries,
                virtualAssetIdentifiers = virtualAssetIdentifiers,
            };

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
                var virtualGroupByKey = aaContext.Settings.groups
                    .OfType<VirtualAddressableAssetGroup>()
                    .ToDictionary(x => x.bundleName);

                HashSet<string> newDependencyEntryKeys = new HashSet<string>();

                for (int i = aaContext.locations.Count - 1; i >= 0; i--)
                {
                    ContentCatalogDataEntry dataEntry = aaContext.locations[i];
                    if (typeof(IAssetBundleResource).IsAssignableFrom(dataEntry.ResourceType))
                    {
                        if (dataEntry.Keys == null || dataEntry.Keys.FirstOrDefault() is not string primaryKey || !virtualGroupByKey.TryGetValue(primaryKey, out var virtualGroup))
                        {
                            continue;
                        }
                        pipeline.Log(LogLevel.Information, $"Virtual Group entry: {dataEntry.InternalId}", $"Key\n{dataEntry.Keys[0]}");
                        dataEntry.Data = virtualGroup.data;
                        foreach (var dependency in virtualGroup.dependencies)
                        {
                            if (newDependencyEntryKeys.Add(dependency.primaryKey))
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
                        HashSet<string> newDependencies = new HashSet<string>();
                        foreach (var dependency in dataEntry.Dependencies.OfType<string>())
                        {
                            if (virtualGroupByKey.TryGetValue(dependency, out var virtualGroup))
                            {
                                newDependencies.UnionWith(virtualGroup.dependencies.Select(x => x.primaryKey));
                            }
                        }
                        dataEntry.Dependencies.AddRange(newDependencies);
                    }
                }
                /*List<ObjectInitializationData> resourceProviderData = (List<ObjectInitializationData>)m_ResourceProviderData.GetValue(this);
                Dictionary<string, string> bundleToInternalId = (Dictionary<string, string>)m_BundleToInternalId.GetValue(this);

                string providerId = typeof(TempAssetBundleProvider).FullName;
                resourceProviderData.RemoveAll(x => x.Id == providerId);
                Dictionary<object, List<object>> tempAssetBundleToAssetDependencies = new Dictionary<object, List<object>>();
                for (int i = aaContext.locations.Count - 1; i >= 0; i--)
                {
                    ContentCatalogDataEntry dataEntry = aaContext.locations[i];
                    if (dataEntry.Provider == providerId)
                    {
                        pipeline.Log(LogLevel.Information, $"Temp asset bundle: {dataEntry.InternalId}", $"Key\n{dataEntry.Keys[0]}");
                        List<object> assetDependencies = new List<object>();
                        foreach (var key in dataEntry.Keys)
                        {
                            tempAssetBundleToAssetDependencies.Add(key, assetDependencies);
                        }
                        bundleToInternalId.Add((string)dataEntry.Keys[0], dataEntry.InternalId);
                        aaContext.locations.RemoveAt(i);
                    }
                }
                providerId = typeof(AssetDependencyProvider).FullName;
                foreach (var dataEntry in aaContext.locations)
                {
                    if (dataEntry.Provider == providerId)
                    {
                        pipeline.Log(LogLevel.Information, $"Asset dependency provider: {dataEntry.InternalId}", $"Asset dependency provider\n{dataEntry.InternalId}");
                        foreach (var dependency in dataEntry.Dependencies)
                        {
                            if (tempAssetBundleToAssetDependencies.TryGetValue(dependency, out var assetDependencies))
                            {
                                assetDependencies.AddRange(dataEntry.Keys);
                            }
                        }
                        dataEntry.Dependencies.Clear();
                        dataEntry.Dependencies.AddRange(dataEntry.Keys);
                        dataEntry.InternalId = string.Empty;
                    }
                }
                foreach (var dataEntry in aaContext.locations)
                {
                    if (dataEntry.Provider != providerId)
                    {
                        for (int i = dataEntry.Dependencies.Count - 1; i >= 0; i--)
                        {
                            if (tempAssetBundleToAssetDependencies.TryGetValue(dataEntry.Dependencies[i], out var assetDependencies))
                            {
                                pipeline.Log(LogLevel.Information, $"Replace dependency {dataEntry.Dependencies[i]} on {dataEntry.InternalId}", $"dependency\n{dataEntry.Dependencies[i]}", $"entry\n{dataEntry.InternalId}");
                                dataEntry.Dependencies.RemoveAt(i);
                                dataEntry.Dependencies.AddRange(assetDependencies);
                            }
                        }
                    }
                }*/
                return ReturnCode.Success;
            }

            ReturnCode PostPacking(IBuildParameters parameters, IDependencyData dependencyData, IWriteData writeData)
            {
                /*HashSet<string> ignoredBundleNames = new HashSet<string>(aaContext.Settings.groups.OfType<VirtualAddressableAssetGroup>().Select(x => x.bundleName));
                writeData.WriteOperations.RemoveAll(x =>
                {
                    return x is AssetBundleWriteOperation writeOperation && ignoredBundleNames.Contains(writeOperation.Info.bundleName);
                });*/
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
    }
}