using Unity.Entities;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public struct SpeciesIdCmp : ISharedComponentData
    {
        public int Value;
    }
}