using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AssetsOfRain
{
    public static class AssetsOfRain
    {
        private static readonly Type CompactLocationType = typeof(ContentCatalogData).GetNestedType("CompactLocation", BindingFlags.NonPublic);
        private static readonly FieldInfo m_Dependency = CompactLocationType.GetField("m_Dependency", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo m_Locator = CompactLocationType.GetField("m_Locator", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo m_DependencyHashCode = CompactLocationType.GetField("m_DependencyHashCode", BindingFlags.Instance | BindingFlags.NonPublic);

        public static AsyncOperationHandle<IResourceLocator> LoadModdedContentCatalogAsync(string catalogPath, string providerSuffix = null)
        {
            var op = Addressables.LoadContentCatalogAsync(catalogPath, true, providerSuffix);
            op.Completed += OnModdedCatalogDataLoaded;
            return op;
        }

        private static void OnModdedCatalogDataLoaded(AsyncOperationHandle<IResourceLocator> op)
        {
            if (op.Result is not ResourceLocationMap locMap)
            {
                return;
            }
            ResourceLocationMap gameLocMap = Addressables.ResourceLocators.OfType<ResourceLocationMap>().FirstOrDefault();
            if (gameLocMap == null)
            {
                return;
            }
            Debug.Log(gameLocMap.Locations.Count);
            string providerId = typeof(AssetDependencyProvider).FullName;
            foreach (var location in locMap.Locations.Values
                .SelectMany(x => x)
                .Where(x => x.ProviderId == providerId && CompactLocationType.IsAssignableFrom(x.GetType())))
            {
                Debug.Log(location.PrimaryKey);
                //string dependency = Path.GetDirectoryName(location.InternalId);
                //m_Dependency.SetValue(location, dependency);
                //m_DependencyHashCode.SetValue(location, dependency.GetHashCode());
                Debug.Log($"    \"{m_Dependency.GetValue(location)}\"");
                m_Locator.SetValue(location, gameLocMap);
            }
        }
    }
}
