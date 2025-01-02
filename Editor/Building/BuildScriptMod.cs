using AssetsOfRain.Editor.Materials;
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
            var virtualAssets = new Dictionary<ObjectIdentifier, VirtualAddressableAssetImporter.Result>();
            var virtualAssetGuids = new HashSet<GUID>();
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
                        virtualAssetGuids.Add(objectId.guid);
                    }
                }
                virtualAssetDependencies[virtualAssetPath] = importer.bundleDependencies;
            }

            ContentPipeline.BuildCallbacks.PostScriptsCallbacks += PostScripts;
            ContentPipeline.BuildCallbacks.PostPackingCallback += PostPacking;
            ContentPipeline.BuildCallbacks.PostWritingCallback += PostWriting;
            var result = base.DoBuild<TResult>(builderInput, aaContext);
            ContentPipeline.BuildCallbacks.PostScriptsCallbacks -= PostScripts;
            ContentPipeline.BuildCallbacks.PostPackingCallback -= PostPacking;
            ContentPipeline.BuildCallbacks.PostWritingCallback -= PostWriting;
            return result;

            ReturnCode PostScripts(IBuildParameters parameters, IBuildResults results)
            {
                MaterialDataStorage.instance.ApplyPersistentShaders();
                return ReturnCode.Success;
            }

            ReturnCode PostPacking(IBuildParameters parameters, IDependencyData dependencyData, IWriteData writeData)
            {
                foreach (var assetBundleWriteOperation in writeData.WriteOperations.OfType<AssetBundleWriteOperation>())
                {
                    var bundleAssets = assetBundleWriteOperation.Info.bundleAssets;
                    for (int i = bundleAssets.Count - 1; i >= 0; i--)
                    {
                        AssetLoadInfo assetLoadInfo = bundleAssets[i];
                        if (virtualAssetGuids.Contains(assetLoadInfo.asset))
                        {
                            bundleAssets.RemoveAt(i);
                            continue;
                        }
                        foreach (var referencedObject in assetLoadInfo.referencedObjects)
                        {
                            if (virtualAssets.TryGetValue(referencedObject, out var virtualAsset))
                            {
                                assetBundleWriteOperation.ReferenceMap.AddMapping(virtualAsset.internalBundleName, virtualAsset.identifier, referencedObject, true);
                            }
                        }
                    }
                    assetBundleWriteOperation.Command.serializeObjects.RemoveAll(x => virtualAssets.ContainsKey(x.serializationObject));
                }
                return ReturnCode.Success;
            }

            ReturnCode PostWriting(IBuildParameters parameters, IDependencyData dependencyData, IWriteData writeData, IBuildResults results)
            {
                int originalLocationCount = aaContext.locations.Count;
                var bundleDependencyKeysMap = new Dictionary<string, HashSet<string>>();
                var allBundleDependencyKeys = new HashSet<string>();

                foreach (var assetBundleWriteOperation in writeData.WriteOperations.OfType<AssetBundleWriteOperation>())
                {
                    HashSet<string> bundleDependencyKeys = new HashSet<string>();

                    var bundleDependencies = assetBundleWriteOperation.Info.bundleAssets
                        .SelectMany(x => x.referencedObjects)
                        .Select(x => AssetDatabase.GUIDToAssetPath(x.guid))
                        .Distinct()
                        .Where(virtualAssetDependencies.ContainsKey)
                        .SelectMany(x => virtualAssetDependencies[x]);

                    foreach (var bundleDependency in bundleDependencies)
                    {
                        if (bundleDependencyKeys.Add(bundleDependency.primaryKey) && allBundleDependencyKeys.Add(bundleDependency.primaryKey))
                        {
                            aaContext.locations.Add(new ContentCatalogDataEntry(
                                type: typeof(IAssetBundleResource),
                                internalId: bundleDependency.internalId,
                                provider: bundleDependency.providerId,
                                keys: new[] { bundleDependency.primaryKey },
                                extraData: bundleDependency.data));
                        }
                    }
                    if (bundleDependencyKeys.Count > 0)
                    {
                        bundleDependencyKeysMap.Add(assetBundleWriteOperation.Info.bundleName, bundleDependencyKeys);
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
                        .Where(bundleDependencyKeysMap.ContainsKey)
                        .SelectMany(x => bundleDependencyKeysMap[x])
                        .Distinct());
                }
                return ReturnCode.Success;
            }
        }
    }
}