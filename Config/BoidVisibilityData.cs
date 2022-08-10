using System;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [Serializable]
    public struct BoidVisibilityData
    {
        public float Distance;
        public float VisionAngle;
    }
}