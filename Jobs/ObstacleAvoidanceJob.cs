using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [BurstCompile]
    public struct ObstacleAvoidanceJob : IJobParallelFor
    {
        // Inputs
        private float _avoidanceDistance;
        [ReadOnly] private CollisionWorld _collisionWorld;
        [ReadOnly] private NativeArray<float3> _posArray;
        [ReadOnly] private NativeArray<float3> _velArray;

        //Outputs
        private NativeArray<float3> _obstacleAvoidanceArray;

        public ObstacleAvoidanceJob(float avoidanceDistance, CollisionWorld collisionWorld,
            NativeArray<float3> posArray, NativeArray<float3> velArray, NativeArray<float3> obstacleAvoidanceArray)
        {
            _avoidanceDistance = avoidanceDistance;
            _collisionWorld = collisionWorld;
            _posArray = posArray;
            _velArray = velArray;
            _obstacleAvoidanceArray = obstacleAvoidanceArray;
        }

        public void Execute(int index)
        {
            float3 pos = _posArray[index];
            float3 vel = _velArray[index];
            float3 dir = math.normalize(vel);

            var castInput = new RaycastInput
            {
                Start = pos,
                End = pos + dir * _avoidanceDistance,
                Filter = CollisionFilter.Default
            };

            // If hit was found then we set obstacle avoidance vector as current dir reflection from surface hit
            // Then we scale it, so the closer to surface the more we use this vector in acceleration calculations
            float3 result = _collisionWorld.CastRay(castInput, out var closestHit)
                ? math.reflect(dir, closestHit.SurfaceNormal) * (1 - closestHit.Fraction)
                : float3.zero;

            _obstacleAvoidanceArray[index] = result;
        }
    }
}