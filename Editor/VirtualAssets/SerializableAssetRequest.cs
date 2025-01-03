using System;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace AssetsOfRain.Editor.VirtualAssets
{
    [Serializable]
    public struct SerializableAssetRequest : IEquatable<SerializableAssetRequest>
    {
        public string primaryKey;
        public string assemblyQualifiedTypeName;

        private IResourceLocation cachedAssetLocation;
        private Type cachedAssetType;

        public Type AssetType
        {
            get
            {
                if (cachedAssetType == null || cachedAssetType.AssemblyQualifiedName != assemblyQualifiedTypeName)
                {
                    cachedAssetType = Type.GetType(assemblyQualifiedTypeName ?? string.Empty);
                }
                return cachedAssetType;
            }
            set
            {
                assemblyQualifiedTypeName = value.AssemblyQualifiedName;
                cachedAssetType = value;
            }
        }

        public IResourceLocation AssetLocation
        {
            get
            {
                if (cachedAssetLocation == null || cachedAssetLocation.PrimaryKey != primaryKey || cachedAssetLocation.ResourceType != AssetType)
                {
                    cachedAssetLocation = Addressables.LoadResourceLocationsAsync(primaryKey ?? string.Empty, AssetType).WaitForCompletion().FirstOrDefault();
                }
                return cachedAssetLocation;
            }
            set
            {
                primaryKey = value.PrimaryKey;
                AssetType = value.ResourceType;
                cachedAssetLocation = value;
            }
        }

        public readonly bool Equals(SerializableAssetRequest other)
        {
            return primaryKey == other.primaryKey && assemblyQualifiedTypeName == other.assemblyQualifiedTypeName;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is SerializableAssetRequest other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(primaryKey, assemblyQualifiedTypeName);
        }
    }
}
