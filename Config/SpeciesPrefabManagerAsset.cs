using System.Collections.Generic;
using GPUInstancer;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [CreateAssetMenu(menuName = "eseas/PerFishSim/SpeciesPrefabManagerAsset", fileName = "SpeciesPrefabManagerAsset",
        order = 0)]
    public class SpeciesPrefabManagerAsset : ScriptableObject
    {
        [SerializeField, InlineEditor(Expanded = true)] 
        private List<SpeciesBehaviourConfigAsset> declaredSpecies;

        public int GetSpeciesCount => declaredSpecies.Count;

        private void OnEnable()
        {
            UpdateSpeciesIds();
        }

        private void OnValidate()
        {
            UpdateSpeciesIds();
        }

        public GPUInstancerPrefabPrototype GetPrefabPrototypeForSpeciesId(int id)
        {
            return declaredSpecies[id].GPUIPrefab.prefabPrototype;
        }

        [Button]
        private void UpdateSpeciesIds()
        {
            if (declaredSpecies == null || declaredSpecies.Count == 0)
            {
                Debug.LogWarning($"No species declared.");
                return;
            }

            int validDeclaredSpecies = 0;
            for (int i = 0; i < declaredSpecies.Count; i++)
            {
                var prefab = declaredSpecies[i].GPUIPrefab;
                if (prefab == null)
                {
                    Debug.LogError($"GPUIPrefab not provided in {declaredSpecies[i].name}!", declaredSpecies[i]);
                    // declaredSpecies.RemoveAt(i);
                    continue;
                }

                // prefabProtoForId.Add(prefab.prefabPrototype);
                declaredSpecies[i].ID = validDeclaredSpecies;
#if UNITY_EDITOR
                EditorUtility.SetDirty(declaredSpecies[i]);
#endif
                validDeclaredSpecies++;
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            // AssetDatabase.SaveAssets();
#endif

            Debug.Log("Species Ids updated.");
        }
    }
}