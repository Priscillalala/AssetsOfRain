using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetsOfRain.Editor.Materials
{
    public class MaterialDataStorage : ScriptableSingleton<MaterialDataStorage>, ISerializationCallbackReceiver
    {
        public readonly Dictionary<int, Shader> materialToPersistentShader = new Dictionary<int, Shader>();

        [SerializeField]
        private List<Material> serializedMaterials = new List<Material>();
        [SerializeField]
        private List<Shader> serializedPersistentShaders = new List<Shader>();

        public void OnBeforeSerialize()
        {
            serializedMaterials.Clear();
            serializedMaterials.AddRange(materialToPersistentShader.Keys.Select(EditorUtility.InstanceIDToObject).Cast<Material>());
            serializedPersistentShaders.Clear();
            serializedPersistentShaders.AddRange(materialToPersistentShader.Values);
        }

        public void OnAfterDeserialize()
        {
            materialToPersistentShader.Clear();
            for (int i = 0; i < serializedMaterials.Count; i++)
            {
                materialToPersistentShader.Add(serializedMaterials[i].GetInstanceID(), serializedPersistentShaders[i]);
            }
        }

        public void ApplyPersistentShaders()
        {
            foreach (var pair in materialToPersistentShader)
            {
                if (EditorUtility.InstanceIDToObject(pair.Key) is not Material material || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material.shader)))
                {
                    continue;
                }
                Shader persistentShader = pair.Value;
                if (persistentShader == null || !persistentShader.isSupported)
                {
                    continue;
                }
                material.shader = persistentShader;
            }
        }
    }
}