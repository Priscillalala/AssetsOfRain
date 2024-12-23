﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.AddressableAssets.Settings;
using ThunderKit.Core.Pipelines;
using System.Threading.Tasks;
using UnityEditor.AddressableAssets;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Paths;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using System.Reflection;
using System;

namespace AssetsOfRain.Editor.Building
{
    [PipelineSupport(typeof(Pipeline))]
    [ManifestProcessor]
    [RequiresManifestDatumType(typeof(AddressablesDefinition))]
    public class StageAddressables : PipelineJob
    {
        private static readonly FieldInfo m_GUID = typeof(AddressableAssetGroup).GetField("m_GUID", BindingFlags.Instance | BindingFlags.NonPublic);

        public AddressableAssetSettings Addressables => AddressableAssetSettingsDefaultObject.Settings;

        [PathReferenceResolver]
        public string BuildArtifactPath = "<AddressablesStaging>";
        public bool compileScripts = false;
        public bool clearHashFile = true;

        public override Task Execute(Pipeline pipeline)
        {
            var definition = pipeline.Manifest.Data.OfType<AddressablesDefinition>().FirstOrDefault();
            if (definition && Addressables)
            {
                string resolvedArtifactPath = BuildArtifactPath.Resolve(pipeline, this);
                string resolvedLoadPath = definition.RuntimeLoadPath.Resolve(pipeline, this);
                Addressables.BuildRemoteCatalog = true;
                Addressables.profileSettings.SetValue(Addressables.activeProfileId, Addressables.RemoteCatalogBuildPath.GetName(Addressables), resolvedArtifactPath);
                Addressables.profileSettings.SetValue(Addressables.activeProfileId, Addressables.RemoteCatalogLoadPath.GetName(Addressables), resolvedLoadPath);
                Addressables.OverridePlayerVersion = pipeline.Manifest.Identity.Name;
                Addressables.ActivePlayerDataBuilderIndex = Addressables.DataBuilders.FindIndex(s => s.GetType() == typeof(BuildScriptMod));
                BuildScriptMod.pipeline = pipeline;

                /*pipeline.Log(LogLevel.Information, "I. B. Addressables groups:");
                foreach (string importedBundleGuid in AssetDatabase.FindAssets($"t:{nameof(ImportedBundle)}"))
                {
                    AddressableAssetEntry entry = Addressables.FindAssetEntry(importedBundleGuid);
                    if (entry == null)
                    {
                        continue;
                    }
                    pipeline.Log(LogLevel.Information, entry.address);
                    pipeline.Log(LogLevel.Information, entry.parentGroup.Name);
                    pipeline.Log(LogLevel.Information, $"GUID: {entry.parentGroup.Guid}");
                    string importedBundlePath = AssetDatabase.GUIDToAssetPath(importedBundleGuid);
                    string assetBundleName = AssetDatabase.LoadAssetAtPath<ImportedBundle>(importedBundlePath).assetBundleName;
                    m_GUID.SetValue(entry.parentGroup, Path.GetFileNameWithoutExtension(assetBundleName));
                }*/

                //Addressables.ActivePlayerDataBuilderIndex = Addressables.DataBuilders.FindIndex(s => s.GetType() == typeof(BuildScriptRoR2));
                //((BuildScriptRoR2)Addressables.ActivePlayerDataBuilder).SetAssetTypeLabels(definition.assetTypeLabels, definition.componentTypeLabels);
                void BuildAddressables()
                {
                    AddressableAssetSettings.BuildPlayerContent(out var result);
                    if (string.IsNullOrEmpty(result.Error))
                    {
                        pipeline.Log(LogLevel.Information, $"Finished Addressables build in {result.Duration} seconds");
                    }
                    else
                    {
                        throw new System.Exception($"Error while building Addressables: {result.Error}");
                        pipeline.Log(LogLevel.Error, $"Error while building Addressables: {result.Error}", $"Error while building Addressables: {result.Error}");
                    }
                }
                if (compileScripts)
                {
                    BuildAddressables();
                }
                else
                {
                    FieldInfo s_SkipCompilePlayerScripts = typeof(BuildScriptPackedMode).GetField("s_SkipCompilePlayerScripts", BindingFlags.Static | BindingFlags.NonPublic);
                    s_SkipCompilePlayerScripts.SetValue(null, true);
                    BuildAddressables();
                    s_SkipCompilePlayerScripts.SetValue(null, false);
                }
                if (clearHashFile)
                {
                    string hashPath = Path.Combine(resolvedArtifactPath, $"catalog_{Addressables.OverridePlayerVersion}.hash");
                    if (File.Exists(hashPath))
                    {
                        File.Delete(hashPath);
                    }
                }
                foreach (string stagingPath in definition.StagingPaths)
                {
                    string resolvedStagingPath = stagingPath.Resolve(pipeline, this);
                    FileUtil.ReplaceDirectory(resolvedArtifactPath, resolvedStagingPath);
                }
            }
            return Task.CompletedTask;
        }
    }
}