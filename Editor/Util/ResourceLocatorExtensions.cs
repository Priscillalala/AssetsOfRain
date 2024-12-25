using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace AssetsOfRain.Editor.Util
{
    public static class ResourceLocatorExtensions
    {
        public static IEnumerable<IResourceLocation> AllLocationsOfType(this IResourceLocator locator, Type type)
        {
            foreach (var key in locator.Keys)
            {
                if (locator.Locate(key, type, out IList<IResourceLocation> locations))
                {
                    foreach (var location in locations)
                    {
                        yield return location;
                    }
                }
            }
        }
    }
}
