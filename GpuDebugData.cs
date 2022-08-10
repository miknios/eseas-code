using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct GpuDebugData
    {
        public float3 cohesionVec;
        public float cohesionLen;
        public float cohesionFrac;
        public float cohesionFinalWeight;
        public float3 separationVec;
        public float separationLen;
        public float separationFrac;
        public float separationFinalWeight;
        public float3 alignmentVec;
        public float alignmentLen;
        public float alignmentFrac;
        public float alignmentFinalWeight;
        public float3 boundAvoidance;
        public float boundAvoidanceFrac;
        public float boundAvoidFinalWeight;
        public float3 obstacleAvoidance;
        public float obstacleAvoidanceFrac;
        public float obstacleAvoidanceFinalWeight;
        public float weightSum;
        public int visibleBoids;
        public float cohesionInfl;
        public float alignmentInfl;
        public float separationInfl;
        public float boundAvoidInfl;
        public float obstacleAvoidInfl;
        public float3 velocity;
        public float speedTarget;
        public float speedCurrent;
        public float dynamicObstacleAvoidanceFrac;
        public int visibleDynamicObstacles;
        public float animTime;
        public float animTimeMultiplier;
        public float angleDiff;
        public float angleFlappingMultiplier;
        public float speedDiff;
        public float speedFlappingMultiplier;
        public float speedAngleMultiplier;
    }
}