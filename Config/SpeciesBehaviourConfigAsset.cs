using GPUInstancer;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [CreateAssetMenu(menuName = "eseas/PerFishSim/SpeciesBehaviourConfig", fileName = "SpeciesBehaviourConfig", order = 0)]
    public class SpeciesBehaviourConfigAsset : ScriptableObject
    {
        [SerializeField, ReadOnly] private int id;
        [SerializeField] private Color color;
        [SerializeField, HideLabel] private SpeciesBehaviourConfig config;
        [SerializeField, Required] private BoidTagBehaviour entityPrefab;
        [SerializeField, Required, InlineEditor(DrawPreview = true, Expanded = true)] 
        private GPUInstancerPrefab gpuiPrefab;

        public int ID
        {
            get => id;
            set => id = value;
        }
        public SpeciesBehaviourConfig Config => config;
        public GameObject EntityPrefab => entityPrefab.gameObject;
        public GPUInstancerPrefab GPUIPrefab => gpuiPrefab;
        public Color Color => color;
    }
}