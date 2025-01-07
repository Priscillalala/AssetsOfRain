using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Paths;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace AssetsOfRain.Editor.Building
{
    [PipelineSupport(typeof(Pipeline))]
    [ManifestProcessor]
    [RequiresManifestDatumType(typeof(AddressablesDefinition))]
    public class StageAddressables : PipelineJob
    {
        public AddressableAssetSettings Addressables => AddressableAssetSettingsDefaultObject.Settings;

        [PathReferenceResolver]
        public string BuildArtifactPath = "<AddressablesStaging>";
        [Tooltip("Addressables will usually compile scripts before every build. This bloats build times with seemingly no benefit")]
        public bool compileScripts = false;
        [Tooltip("Addressables generates a hash file to quickly compare catalog versions, but this file is not relevent for mods")]
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
                // ContiguousBundles was causing problems with resolving virtual asset references
                Addressables.ContiguousBundles = false;
                if (!Addressables.DataBuilders.OfType<BuildScriptMod>().Any())
                {
                    Addressables.AddDataBuilder(AssetDatabase.LoadAssetAtPath<BuildScriptMod>(AssetsOfRain.PACKAGE_ASSETS_DIRECTORY + "/Addressables/BuildScriptMod.asset"));
                }
                Addressables.ActivePlayerDataBuilderIndex = Addressables.DataBuilders.FindIndex(s => s is BuildScriptMod);
                BuildScriptMod.pipeline = pipeline;

                void BuildAddressables()
                {
                    AddressableAssetSettings.BuildPlayerContent(out var result);
                    if (string.IsNullOrEmpty(result.Error))
                    {
                        pipeline.Log(LogLevel.Information, $"Finished Addressables build in {result.Duration} seconds");
                    }
                    else
                    {
                        pipeline.Log(LogLevel.Error, $"Error while building Addressables: {result.Error}", $"Build Error\n{result.Error}");
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
                    File.Delete(hashPath);
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