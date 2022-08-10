using Unity.Entities;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public static class EntityQueryHelper
    {
        public static ComponentType[] simulatedBoidComponentTypes =
        {
            ComponentType.ReadOnly<BoidTagCmp>(),
            ComponentType.ReadOnly<BoidGroupDataCmp>(),
            ComponentType.Exclude<NotSimulatedTagCmp>(),
        };
    }
}