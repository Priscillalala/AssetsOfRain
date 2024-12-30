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
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using LogLevel = ThunderKit.Core.Pipelines.LogLevel;

namespace AssetsOfRain.Editor.Building
{
    [CreateAssetMenu(fileName = "BuildScriptMod.asset", menuName = "Addressables/Content Builders/Build Modded Content")]
    public class BuildScriptMod : BuildScriptPackedMode
    {
        private static readonly FieldInfo m_AllBundleInputDefs = typeof(BuildScriptPackedMode).GetField("m_AllBundleInputDefs", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo m_OutputAssetBundleNames = typeof(BuildScriptPackedMode).GetField("m_OutputAssetBundleNames", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo m_BundleToInternalId = typeof(BuildScriptPackedMode).GetField("m_BundleToInternalId", BindingFlags.Instance | BindingFlags.NonPublic);

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
                Dictionary<string, string> bundleToInternalId = (Dictionary<string, string>)m_BundleToInternalId.GetValue(this);
                string providerId = typeof(TempAssetBundleProvider).FullName;
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
                }
                //((ModdedAddressableAssetsBuildContext)aaContext).LogDependencies();
                return ReturnCode.Success;
            }

            ContentPipeline.BuildCallbacks.PostWritingCallback = PostWriting;
            pipeline.Log(LogLevel.Information, "Continuing build..");
            var buildResult = base.DoBuild<TResult>(builderInput, aaContext);
            ContentPipeline.BuildCallbacks.PostWritingCallback = null;
            //((ModdedAddressableAssetsBuildContext)aaContext).LogDependencies();
            return buildResult;
            /*var importedBundlePaths = AssetDatabase.FindAssets($"t:{nameof(ImportedBundle)}").Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (string importedBundlePath in importedBundlePaths)
            {
                string assetBundleName = AssetDatabase.LoadAssetAtPath<ImportedBundle>(importedBundlePath).assetBundleName;
                allBundleInputDefs.Add(new AssetBundleBuild
                {
                    assetBundleName = assetBundleName,
                    assetNames = new string[] { importedBundlePath },
                });
                outputAssetBundleNames.Add(assetBundleName);
            }*/

            /*foreach (string importedBundleGuid in AssetDatabase.FindAssets($"t:{nameof(ImportedBundle)}"))
            {
                AddressableAssetEntry entry = aaContext.Settings.FindAssetEntry(importedBundleGuid);
                if (entry == null)
                {
                    continue;
                }

                string assetGroupGuid = entry.parentGroup.Guid;
                if (!aaContext.bundleToAssetGroup.ContainsValue(assetGroupGuid))
                {
                    continue;
                }

                string originalAssetBundleName = aaContext.bundleToAssetGroup.FirstOrDefault(x => x.Value == assetGroupGuid).Key;
                string importedBundlePath = AssetDatabase.GUIDToAssetPath(importedBundleGuid);
                string assetBundleName = AssetDatabase.LoadAssetAtPath<ImportedBundle>(importedBundlePath).assetBundleName;

                aaContext.bundleToAssetGroup.Remove(originalAssetBundleName);
                aaContext.bundleToAssetGroup.Add(assetBundleName, assetGroupGuid);

                for (int i = 0; i < allBundleInputDefs.Count; i++)
                {
                    var bundleInputDef = allBundleInputDefs[i];
                    if (bundleInputDef.assetBundleName == originalAssetBundleName)
                    {
                        bundleInputDef.assetBundleName = assetBundleName;
                        allBundleInputDefs[i] = bundleInputDef;
                    }
                }
            }*/
            /*List<string> outputAssetBundleNames = (List<string>)m_OutputAssetBundleNames.GetValue(this);

            pipeline.Log(LogLevel.Information, "allBundleInputDefs:");
            foreach (var bundleDef in allBundleInputDefs)
            {
                pipeline.Log(LogLevel.Information, bundleDef.assetBundleName);
                pipeline.Log(LogLevel.Information, string.Join(", ", bundleDef.assetNames));
            }
            pipeline.Log(LogLevel.Information, "outputAssetBundleNames:");
            foreach (var outputAssetBundleName in outputAssetBundleNames)
            {
                pipeline.Log(LogLevel.Information, outputAssetBundleName);
            }
            pipeline.Log(LogLevel.Information, "bundleToAssetGroup:");
            foreach (var bundle in aaContext.bundleToAssetGroup.Keys)
            {
                pipeline.Log(LogLevel.Information, bundle);
            }*/
            /*foreach (var bundleToAssetGroup in aaContext.bundleToAssetGroup)
            {
                var group = aaContext.Settings.FindGroup(x => x && x.Guid == bundleToAssetGroup.Value);
                var importedBundle = group.entries.Select(x => x.MainAsset).OfType<ImportedBundle>().FirstOrDefault();
                if (importedBundle)
                {
                    string originalAssetBundleName = bundleToAssetGroup.Key;

                }
            }*/
        }

    }
}