using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AssetsOfRain.Editor.Materials
{
    public class MaterialDataStorage : ScriptableSingleton<MaterialDataStorage>, ISerializationCallbackReceiver
    {
        public readonly Dictionary<int, Shader> materialToPersistentShader = new Dictionary<int, Shader>();

        [SerializeField]
        private List<int> serializedMaterialIds = new List<int>();
        [SerializeField]
        private List<Shader> serializedPersistentShaders = new List<Shader>();

        public void OnEnable()
        {
            
        }

        public void OnDisable()
        {
            
        }

        public void OnBeforeSerialize()
        {
            serializedMaterialIds.Clear();
            serializedMaterialIds.AddRange(materialToPersistentShader.Keys);
            serializedPersistentShaders.Clear();
            serializedPersistentShaders.AddRange(materialToPersistentShader.Values);
        }

        public void OnAfterDeserialize()
        {
            materialToPersistentShader.Clear();
            for (int i = 0; i < serializedMaterialIds.Count; i++)
            {
                materialToPersistentShader.Add(serializedMaterialIds[i], serializedPersistentShaders[i]);
            }
        }
    }
}