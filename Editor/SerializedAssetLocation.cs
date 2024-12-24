using System;
using UnityEditor;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace AssetsOfRain.Editor
{
    [Serializable]
    public struct SerializedAssetLocation
    {
        public string primaryKey;
        public string assemblyQualifiedTypeName;

        private string cachedAssemblyQualifiedTypeName;
        private Type cachedAssetType;

        public Type AssetType
        {
            get
            {
                if (cachedAssemblyQualifiedTypeName != assemblyQualifiedTypeName)
                {
                    cachedAssemblyQualifiedTypeName = assemblyQualifiedTypeName ?? string.Empty;
                    cachedAssetType = Type.GetType(cachedAssemblyQualifiedTypeName);
                }
                return cachedAssetType;
            }
            set
            {
                assemblyQualifiedTypeName = cachedAssemblyQualifiedTypeName = value.AssemblyQualifiedName;
                cachedAssetType = value;
            }
        }

        public SerializedAssetLocation(IResourceLocation assetLocation)
        {
            primaryKey = assetLocation.PrimaryKey;
            assemblyQualifiedTypeName = cachedAssemblyQualifiedTypeName = assetLocation.ResourceType.AssemblyQualifiedName;
            cachedAssetType = assetLocation.ResourceType;
        }
    }
}
