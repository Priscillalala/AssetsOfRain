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
        private List<Material> serializedMaterialIds = new List<Material>();
        [SerializeField]
        private List<Shader> serializedPersistentShaders = new List<Shader>();

        public void OnBeforeSerialize()
        {
            serializedMaterialIds.Clear();
            serializedMaterialIds.AddRange(materialToPersistentShader.Keys.Select(EditorUtility.InstanceIDToObject).Cast<Material>());
            serializedPersistentShaders.Clear();
            serializedPersistentShaders.AddRange(materialToPersistentShader.Values);
        }

        public void OnAfterDeserialize()
        {
            materialToPersistentShader.Clear();
            for (int i = 0; i < serializedMaterialIds.Count; i++)
            {
                materialToPersistentShader.Add(serializedMaterialIds[i].GetInstanceID(), serializedPersistentShaders[i]);
            }
        }
    }
}