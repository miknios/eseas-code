using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public class BoidGroupSpawnSystem : SystemBase
    {
        private GpuDataManager _gpuDataManager;
        private EntityCommandBufferSystem _commandBufferSystem;
        private NativeArray<float3> _groupPosArray;
        private NativeArray<float3> _groupVelArray;
        private NativeArray<float3> _groupObstacleAvoidanceArray;
        private NativeArray<GpuDebugData> _groupDebugArray;

        protected override void OnCreate()
        {
            _gpuDataManager = GpuDataManager.Instance;
            _commandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();

            var spawnManager = GlobalAssetHolder.Instance.BoidSpawnManager;
            int maxBoidInGroupCount = spawnManager.MaxBoidInGroupCount;
            _groupPosArray = new NativeArray<float3>(maxBoidInGroupCount, Allocator.Persistent);
            _groupVelArray = new NativeArray<float3>(maxBoidInGroupCount, Allocator.Persistent);
            _groupObstacleAvoidanceArray =
                new NativeArray<float3>(maxBoidInGroupCount, Allocator.Persistent);
            _groupDebugArray = new NativeArray<GpuDebugData>(maxBoidInGroupCount, Allocator.Persistent);
            for (int i = 0; i < maxBoidInGroupCount; i++)
            {
                _groupDebugArray[i] = default;
                _groupObstacleAvoidanceArray[i] = float3.zero;
            }
        }

        protected override void OnDestroy()
        {
            _groupPosArray.Dispose();
            _groupVelArray.Dispose();
            _groupObstacleAvoidanceArray.Dispose();
            _groupDebugArray.Dispose();
        }

        protected override void OnUpdate()
        {
            var ecb = _commandBufferSystem.CreateCommandBuffer();
            var groupPosArray = _groupPosArray;
            var groupVelArray = _groupVelArray;
            var groupDebugArray = _groupDebugArray;


            // System processes all entities with BoidGroupSpawnerCmp which contain spawn data
            Entities
                .ForEach((Entity e, BoidGroupSpawnerAuthoring spawnerMb, in BoidGroupSpawnerCmp spawnerCmp) =>
                {
                    int groupCount = spawnerCmp.groupCount;
                    int groupId = _gpuDataManager.RegisterGroup(groupCount);
                    
                    SpawnZone currentSpawnZone = spawnerMb.GetRandomSpawnZone();
                    
                    for (int i = 0; i < groupCount; i++)
                    {
                        var boidEntity = ecb.Instantiate(spawnerCmp.prefab);

                        if (currentSpawnZone.IsFull)
                            currentSpawnZone = spawnerMb.GetRandomSpawnZone();
                        
                        float3 pos = currentSpawnZone.GetRandomPos();
                        currentSpawnZone.RegisterSpawn();
                        
                        float3 dir = currentSpawnZone.transform.forward;
                        quaternion rot = quaternion.LookRotation(dir, new float3(0, 1, 0));
                        float speed = spawnerCmp.behaviourConfig.speed;
                        float3 vel = dir * speed;

                        // Assign initial data
                        ecb.SetComponent(boidEntity, new Translation {Value = pos});
                        ecb.SetComponent(boidEntity, new Rotation {Value = rot});
                        ecb.SetComponent(boidEntity, new VelocityCmp {Value = vel});
                        ecb.AddComponent(boidEntity, new NonUniformScale {Value = spawnerCmp.boidScale});

                        ecb.AddSharedComponent(boidEntity, new BoidGroupDataCmp
                        {
                            groupId = groupId,
                            speciesId = spawnerCmp.speciesId,
                            behaviourConfig = spawnerCmp.behaviourConfig,
                            boundsCenter = spawnerCmp.centerPos,
                            boundsSize = spawnerCmp.movementBoundsSize,
                            modelScale = spawnerCmp.boidScale
                        });
                        ecb.AddSharedComponent(boidEntity, new SpeciesIdCmp {Value = spawnerCmp.speciesId});

                        groupPosArray[i] = pos;
                        groupVelArray[i] = vel;
                    }

                    // Assign initial group data to GPU buffers
                    _gpuDataManager.UpdateGroupPositions(groupId, groupPosArray);
                    _gpuDataManager.UpdateGroupVelocities(groupId, groupVelArray);
                    _gpuDataManager.UpdateGroupObstacleAvoidanceData(groupId, _groupObstacleAvoidanceArray);
                    _gpuDataManager.UpdateGroupDebugData(groupId, groupDebugArray);

                    // Destroy processed spawner entity
                    ecb.DestroyEntity(e);
                })
                .WithoutBurst()
                .Run();

            _commandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}