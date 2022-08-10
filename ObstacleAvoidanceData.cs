using System;
using UnityEngine;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [Serializable]
    public struct ObstacleAvoidanceData
    {
        public float distance;
        public float rayCorrectionDistance;
    }
}