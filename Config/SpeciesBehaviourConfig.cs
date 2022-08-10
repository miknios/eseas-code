using System;
using Samples.Boids.Boids;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [Serializable]
    public struct SpeciesBehaviourConfig
    {
        public float speed;
        public int visibleBoidMax;
        public BoidVisibilityData visibilityData;
        public float separationTreshold;
        public BoidRuleWeightData ruleWeightData;
    }
}