using System;
using System.Reflection;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace AssetsOfRain.Editor.Util
{
    public static class ResourceProviderExtensions
    {
        private static readonly MethodInfo ProvideResourceInternal = typeof(ResourceManager).GetMethod("ProvideResource", BindingFlags.Instance | BindingFlags.NonPublic);

        public static AsyncOperationHandle ProvideResource(this ResourceManager resourceManager, IResourceLocation location, Type desiredType = null, bool releaseDependenciesOnFailure = true)
        {
            return (AsyncOperationHandle)ProvideResourceInternal.Invoke(resourceManager, new object[] { location, desiredType, releaseDependenciesOnFailure });
        }
    }
}
