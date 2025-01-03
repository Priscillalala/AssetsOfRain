using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetsOfRain.Editor.VirtualAssets.VirtualShaders
{
    // Remember which virtual shader asset a material references through domain reloads, so
    // we can re-load the appropriate addressable shader after
    public class VirtualShaderDataStorage : ScriptableSingleton<VirtualShaderDataStorage>, ISerializationCallbackReceiver
    {
        public readonly Dictionary<int, Shader> materialToShaderAsset = new Dictionary<int, Shader>();

        [SerializeField]
        private List<Material> serializedMaterials = new List<Material>();
        [SerializeField]
        private List<Shader> serializedShaderAssets = new List<Shader>();

        public void OnBeforeSerialize()
        {
            serializedMaterials.Clear();
            serializedMaterials.AddRange(materialToShaderAsset.Keys.Select(EditorUtility.InstanceIDToObject).Cast<Material>());
            serializedShaderAssets.Clear();
            serializedShaderAssets.AddRange(materialToShaderAsset.Values);
        }

        public void OnAfterDeserialize()
        {
            materialToShaderAsset.Clear();
            for (int i = 0; i < serializedMaterials.Count; i++)
            {
                materialToShaderAsset.Add(serializedMaterials[i].GetInstanceID(), serializedShaderAssets[i]);
            }
        }
    }
}