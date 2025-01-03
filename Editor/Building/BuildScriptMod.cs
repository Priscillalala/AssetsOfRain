using AssetsOfRain.Editor.VirtualAssets;
using AssetsOfRain.Editor.VirtualAssets.VirtualShaders;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.WriteTypes;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace AssetsOfRain.Editor.Building
{
    // A version of the default build script that can handle virtual assets
    [CreateAssetMenu(fileName = "BuildScriptMod.asset", menuName = "Addressables/Content Builders/Build Modded Content")]
    public class BuildScriptMod : BuildScriptPackedMode
    {
        public override string Name => "Build Modded Content";

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

            // Assets are evaluated after scripts so this is a good place to prepare our materials for building
            // Materials that are referencing loaded addressable shaders need to be given valid shader assets to build against
            ReturnCode PostScripts(IBuildParameters parameters, IBuildResults results)
            {
                foreach (var pair in VirtualShaderDataStorage.instance.materialToShaderAsset)
                {
                    if (EditorUtility.InstanceIDToObject(pair.Key) is not Material material || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material.shader)))
                    {
                        continue;
                    }
                    Shader shaderAsset = pair.Value;
                    if (shaderAsset == null || !shaderAsset.isSupported)
                    {
                        continue;
                    }
                    material.shader = shaderAsset;
                }
                return ReturnCode.Success;
            }

            // Before any assetbundles are created, edit the write data so our virtual assets resolve to actual assets at runtime
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
                                // Tell our bundle where to find this virtual asset at runtime
                                // If the virtual asset was included in this bundle, it will also be removed
                                assetBundleWriteOperation.ReferenceMap.AddMapping(virtualAsset.internalBundleName, virtualAsset.identifier, referencedObject, true);
                            }
                        }
                    }
                    // Might be unnecessary but it doesn't hurt
                    assetBundleWriteOperation.Command.serializeObjects.RemoveAll(x => virtualAssets.ContainsKey(x.serializationObject));
                }
                return ReturnCode.Success;
            }

            // The initial addressable locations list is generated during writing, but the catalog is
            // generated after the build, so this is the perfect place to inject new locations
            ReturnCode PostWriting(IBuildParameters parameters, IDependencyData dependencyData, IWriteData writeData, IBuildResults results)
            {
                int originalLocationCount = aaContext.locations.Count;
                // Addressable dependencies are calculated per-bundle rather than per-asset, so we
                // map existing bundle keys to additional bundle dependencies from virtual assets
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
                    // If the asset depended on the existing bundle, it should inherit the additional bundle dependencies
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