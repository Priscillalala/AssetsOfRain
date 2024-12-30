using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using ThunderKit.Core.Pipelines;
using LogLevel = ThunderKit.Core.Pipelines.LogLevel;

namespace AssetsOfRain.Editor.Building
{
    public class ModdedAddressableAssetsBuildContext : AddressableAssetsBuildContext, IDeterministicIdentifiers
    {
        private readonly Unity5PackedIdentifiers deterministicIdentifier = new Unity5PackedIdentifiers();

        public Dictionary<ObjectIdentifier, long> virtualAssetIdentifiers;

        public string GenerateInternalFileName(string name)
        {
            return deterministicIdentifier.GenerateInternalFileName(name);
        }

        public long SerializationIndexFromObjectIdentifier(ObjectIdentifier objectID)
        {
            if (virtualAssetIdentifiers != null && virtualAssetIdentifiers.TryGetValue(objectID, out long identifier))
            {
                return identifier;
            }
            return deterministicIdentifier.SerializationIndexFromObjectIdentifier(objectID);
        }

        public void LogDependencies()
        {
            BuildScriptMod.pipeline.Log(LogLevel.Information, "LogDependencies:");
            foreach (var location in locations)
            {
                BuildScriptMod.pipeline.Log(LogLevel.Information, $"Location {location.InternalId}, keys: {string.Join(", ", location.Keys)}");
                foreach (var dependency in location.Dependencies)
                {
                    BuildScriptMod.pipeline.Log(LogLevel.Information, $"Dependency is {dependency}");
                }
            }
        }
    }
}