using Unity.Entities;
using Unity.Mathematics;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public struct BoidGroupSpawnerCmp : IComponentData
    {
        public Entity prefab;
        public int speciesId;
        public int groupCount;
        public float3 centerPos;
        public float3 movementBoundsSize;
        public float3 boidScale;
        public SpeciesBehaviourConfig behaviourConfig;
    }
}