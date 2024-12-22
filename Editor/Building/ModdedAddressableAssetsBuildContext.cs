using System;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;

namespace AssetsOfRain.Editor.Building
{
    public class ModdedAddressableAssetsBuildContext : AddressableAssetsBuildContext, IDeterministicIdentifiers
    {
        public Unity5PackedIdentifiers deterministicIdentifier = new Unity5PackedIdentifiers();

        public string GenerateInternalFileName(string name)
        {
            return deterministicIdentifier.GenerateInternalFileName(name);
        }

        public long SerializationIndexFromObjectIdentifier(ObjectIdentifier objectID)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(objectID.guid);
            if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) == typeof(ExternalAssetsMap))
            {
                ExternalAssetsMap externalAssetsMap = AssetDatabase.LoadAssetAtPath<ExternalAssetsMap>(assetPath);
                if (externalAssetsMap != null && externalAssetsMap.GuidToId.TryGetValue(objectID.guid.ToString(), out long identifier))
                {
                    return identifier;
                }
            }
            return deterministicIdentifier.SerializationIndexFromObjectIdentifier(objectID);
        }
    }
}