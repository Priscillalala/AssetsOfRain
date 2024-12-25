using System;
using UnityEditor;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace AssetsOfRain.Editor.VirtualAssets
{
    [Serializable]
    public struct SerializableAssetRequest : IEquatable<SerializableAssetRequest>
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
