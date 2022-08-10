using Unity.Entities;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public struct GpuDebugDataCmp : IComponentData
    {
        public GpuDebugData Value;
    }
}