using System;
using Unity.Mathematics;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [Serializable]
    public struct BoidRuleWeightData
    {
        public float CohesionWeight;
        public float AlignmentWeight;
        public float SeparatonWeight;
    }
}