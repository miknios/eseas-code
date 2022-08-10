using System.Collections.Generic;
using Drawing;
using TerrainScripts;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public struct ObstacleAvoidanceWriteEntry
    {
        public JobHandle jobHandle;
        public int groupId;
        public NativeArray<float3> data;
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class UpdateObstacleAvoidanceOnGpuSystem : SystemBase
    {
        private GpuDataManager _gpuDataManager;

        private CalculateObstacleAvoidanceForGpuSystem _calculateSystem;

        protected override void OnCreate()
        {
            _gpuDataManager = GpuDataManager.Instance;
            _calculateSystem = World.GetOrCreateSystem<CalculateObstacleAvoidanceForGpuSystem>();
        }

        protected override void OnUpdate()
        {
            int entryCount = _calculateSystem.entries.Count;
            for (int i = 0; i < entryCount; i++)
            {
                var entry = _calculateSystem.entries.Dequeue();

                if (entry.jobHandle.IsCompleted)
                {
                    entry.jobHandle.Complete();
                    _gpuDataManager.UpdateGroupObstacleAvoidanceData(entry.groupId, entry.data);
                    entry.data.Dispose();
                }
                else
                {
                    _calculateSystem.entries.Enqueue(entry);
                }
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    public class CalculateObstacleAvoidanceForGpuSystem : SystemBase
    {
        private BoidSettings _boidSettings;
        private GpuDataManager _gpuDataManager;

        private List<BoidGroupDataCmp> _boidGroups;
        private EntityQuery _boidQuery;
        private EntityQuery _terrainQuery;
        private BuildPhysicsWorld _buildPhysicsWorldSys;
        private NativeArray<int> _lastReadDataVer;
        private NativeArray<float3> _directionsToCheck;

        public Queue<ObstacleAvoidanceWriteEntry> entries;

        protected override void OnCreate()
        {
            _boidSettings = GlobalAssetHolder.Instance.BoidSettings;
            _gpuDataManager = GpuDataManager.Instance;
            var spawnDataManager = GlobalAssetHolder.Instance.BoidSpawnManager;

            _boidGroups = new List<BoidGroupDataCmp>(spawnDataManager.AllGroupCount);
            _boidQuery = GetEntityQuery(EntityQueryHelper.simulatedBoidComponentTypes);
            _terrainQuery = GetEntityQuery(ComponentType.ReadOnly<TerrainTagCmp>());
            _buildPhysicsWorldSys = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>();

            _lastReadDataVer = new NativeArray<int>(spawnDataManager.AllGroupCount, Allocator.Persistent);
            for (int i = 0; i < _lastReadDataVer.Length; i++)
            {
                _lastReadDataVer[i] = -1;
            }

            entries = new Queue<ObstacleAvoidanceWriteEntry>(spawnDataManager.AllGroupCount);

            // Init directions to check
            _directionsToCheck =
                new NativeArray<float3>(_boidSettings.ObstacleAvoidanceDirectionsCount, Allocator.Persistent);

            float goldenRatio = (1 + math.sqrt(5)) / 2;

            float angleIncrement = math.PI * 2 * goldenRatio;
            for (int i = 0; i < _boidSettings.ObstacleAvoidanceDirectionsCount; i++)
            {
                float t = (float) i / _boidSettings.ObstacleAvoidanceDirectionsCount;
                float inclination = Mathf.Acos(1 - 2 * t);
                float azimuth = angleIncrement * i;

                float x = Mathf.Sin(inclination) * Mathf.Cos(azimuth);
                float y = Mathf.Sin(inclination) * Mathf.Sin(azimuth);
                float z = Mathf.Cos(inclination);
                _directionsToCheck[i] = new Vector3(x, y, z);
            }
        }

        protected override void OnDestroy()
        {
            _lastReadDataVer.Dispose();
            _directionsToCheck.Dispose();
        }

        // TODO: optimize waiting for every job to complete on main thread in loop
        protected override void OnUpdate()
        {
            _boidGroups.Clear();
            EntityManager.GetAllUniqueSharedComponentData(_boidGroups);
            ref var collisionWorld = ref _buildPhysicsWorldSys.PhysicsWorld.CollisionWorld;
            Entity terrainEntity = _terrainQuery.CalculateEntityCount() == 1
                ? _terrainQuery.GetSingletonEntity()
                : Entity.Null;

            for (int i = 0; i < _boidGroups.Count; i++)
            {
                _boidQuery.ResetFilter();

                BoidGroupDataCmp groupData = _boidGroups[i];
                _boidQuery.AddSharedComponentFilter(groupData);
                if (_boidQuery.IsEmpty)
                    continue;

                int groupId = groupData.groupId;
                int lastReadDataVerGpu = _gpuDataManager.PosReadSyncBuffer.GetLastReadDataVerForGroup(groupId);
                if (_lastReadDataVer[groupId] == lastReadDataVerGpu)
                    continue;

                _lastReadDataVer[groupId] = lastReadDataVerGpu;
                int boidInGroupCount = _gpuDataManager.GetBoidsInGroupCount(groupId);

                // Fill pos and vel arrays
                NativeArray<float3> posArray =
                    new NativeArray<float3>(boidInGroupCount, Allocator.TempJob);
                NativeArray<float3> velArray =
                    new NativeArray<float3>(boidInGroupCount, Allocator.TempJob);

                var copyPosAndVelHandle = Entities
                    .WithName("CopyPos")
                    .WithSharedComponentFilter(groupData)
                    .WithNone<NotSimulatedTagCmp>()
                    .WithAll<BoidTagCmp>()
                    .ForEach((int entityInQueryIndex, in Translation pos, in VelocityCmp vel) =>
                    {
                        posArray[entityInQueryIndex] = pos.Value;
                        velArray[entityInQueryIndex] = vel.Value;
                    })
                    .ScheduleParallel(Dependency);

                NativeArray<float3> avoidanceArray = new NativeArray<float3>(boidInGroupCount, Allocator.TempJob);
                var avoidanceJob = new ObstacleAvoidancePointsOnSphere(_boidSettings.ObstacleAvoidanceData.distance,
                    _boidSettings.ObstacleAvoidanceData.rayCorrectionDistance, _boidSettings.BoidCollisionCastRadius,
                    terrainEntity, collisionWorld, _directionsToCheck, posArray, velArray, avoidanceArray);
#if BOID_DEBUG
                var commandBuilder = DrawingManager.GetBuilder();
                commandBuilder.Preallocate(groupCount * _boidSettings.ObstacleAvoidanceDirectionsCount);
                commandBuilder.PushLineWidth(10);
                avoidanceJob.commandBuilder = commandBuilder;
#endif
                var avoidanceHandle = avoidanceJob.Schedule(boidInGroupCount, 1, copyPosAndVelHandle);
                _buildPhysicsWorldSys.AddInputDependencyToComplete(avoidanceHandle);

                avoidanceHandle = posArray.Dispose(avoidanceHandle);
                avoidanceHandle = velArray.Dispose(avoidanceHandle);
                entries.Enqueue(new ObstacleAvoidanceWriteEntry()
                {
                    groupId = groupId,
                    jobHandle = avoidanceHandle,
                    data = avoidanceArray
                });

                Dependency = avoidanceHandle;

#if BOID_DEBUG
                avoidanceHandle.Complete();
                commandBuilder.PopLineWidth();
                commandBuilder.Dispose();
#endif
            }
        }

#if (BOID_DEBUG == false)
        [BurstCompile]
#endif
        private struct ObstacleAvoidancePointsOnSphere : IJobParallelFor
        {
#if BOID_DEBUG
            public CommandBuilder commandBuilder;
#endif

            private float _avoidanceDistance;
            private float _correctionRayDistance;
            private float _boidRadius;
            private Entity _terrainEntity;
            [ReadOnly] private CollisionWorld _collisionWorld;
            [ReadOnly] private NativeArray<float3> _directionsToCheck;
            [ReadOnly] private NativeArray<float3> _velArray;
            [ReadOnly] private NativeArray<float3> _posArray;

            private NativeArray<float3> _obstacleAvoidance;

            public ObstacleAvoidancePointsOnSphere(float avoidanceDistance, float correctionRayDistance,
                float boidRadius, Entity terrainEntity,
                CollisionWorld collisionWorld, NativeArray<float3> directionsToCheck, NativeArray<float3> posArray,
                NativeArray<float3> velArray, NativeArray<float3> obstacleAvoidanceArray)
            {
                _avoidanceDistance = avoidanceDistance;
                _boidRadius = boidRadius;
                _terrainEntity = terrainEntity;
                _collisionWorld = collisionWorld;
                _directionsToCheck = directionsToCheck;
                _posArray = posArray;
                _velArray = velArray;
                _obstacleAvoidance = obstacleAvoidanceArray;
                _correctionRayDistance = correctionRayDistance;
#if BOID_DEBUG
                commandBuilder = default;
#endif
            }

            public void Execute(int index)
            {
                float3 pos = _posArray[index];
                float3 dir = math.normalizesafe(_velArray[index]);

                float3 avoidanceVec = float3.zero;
                
                // If there is an obstacle in front of the boid look for avoidance vector
                if (_collisionWorld.SphereCast(pos, _boidRadius, dir, _avoidanceDistance, out var hit, CollisionFilter.Default))
                {
                    bool dirFound = false;

#if BOID_DEBUG
                    commandBuilder.Ray(pos, hit.SurfaceNormal, Color.yellow);
#endif

                    bool shouldEscapeTerrainCollider = hit.Entity == _terrainEntity &&
                                                       math.dot(new float3(0, 1, 0), hit.SurfaceNormal) < 0;
                    if (!shouldEscapeTerrainCollider)
                    {
                        for (int i = 0; i < _directionsToCheck.Length; i++)
                        {
                            float3 dirToCheck = _directionsToCheck[i];
                            var rot = quaternion.LookRotation(dir, math.up());
                            float3 dirWorldSpace = math.rotate(rot, dirToCheck);

                            if (!_collisionWorld.SphereCast(pos, _boidRadius, dirWorldSpace, _correctionRayDistance,
                                CollisionFilter.Default))
                            {
                                float3 avoidanceDir =
                                    math.normalizesafe(math.lerp(dirWorldSpace, hit.SurfaceNormal, 0.75f));
                                avoidanceVec = avoidanceDir * (1 - hit.Fraction);
                                dirFound = true;
#if BOID_DEBUG
                                commandBuilder.Ray(pos, dirWorldSpace * _correctionRayDistance, Color.green);
#endif
                                break;
                            }
#if BOID_DEBUG
                            else
                            {
                                commandBuilder.Ray(pos, dirWorldSpace * _correctionRayDistance, Color.red);
                            }
#endif
                        }

                        // If no direction found - escape collider with closest surface normal
                        if (!dirFound)
                        {
                            _collisionWorld.Bodies[hit.RigidBodyIndex].CalculateDistance(new PointDistanceInput
                            {
                                Position = pos,
                                MaxDistance = 10,
                                Filter = CollisionFilter.Default
                            }, out var escapeColliderHit);

                            avoidanceVec = escapeColliderHit.SurfaceNormal;
                        }
                    }
                    // Escape terrain collider
                    else
                    {
                        avoidanceVec = -hit.SurfaceNormal;
#if BOID_DEBUG
                        commandBuilder.Ray(pos, avoidanceVec * _avoidanceDistance, Color.blue);
#endif
                    }
                }

                _obstacleAvoidance[index] = avoidanceVec;
            }
        }
    }
}