using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public abstract class CopyDataFromGpuSystem : SystemBase
    {
        private GpuDataManager _gpuDataManager;

        private EntityQuery _boidQuery;
        private List<BoidGroupDataCmp> _boidGroups;
        
        private JobHandle[] _copyPosHandleArray;
        private JobHandle[] _copyVelHandleArray;
        private JobHandle[] _copyDebugHandleArray;
        
        protected override void OnCreate()
        {
            _gpuDataManager = GpuDataManager.Instance;
            _boidQuery = GetEntityQuery(EntityQueryHelper.simulatedBoidComponentTypes);
            
            var spawnDataManager = GlobalAssetHolder.Instance.BoidSpawnManager;
            _boidGroups = new List<BoidGroupDataCmp>(spawnDataManager.AllGroupCount);
            _copyPosHandleArray = new JobHandle[spawnDataManager.AllGroupCount];
            _copyVelHandleArray = new JobHandle[spawnDataManager.AllGroupCount];
            _copyDebugHandleArray = new JobHandle[spawnDataManager.AllGroupCount];
        }

        protected override void OnUpdate()
        {
            // Complete all copy pos jobs before arrays are invalidated
            for (int i = 0; i < _copyPosHandleArray.Length; i++)
            {
                var handle = _copyPosHandleArray[i];
                handle.Complete();
                _copyPosHandleArray[i] = handle;
            }
            
            // Complete all copy vel jobs before arrays are invalidated
            for (int i = 0; i < _copyVelHandleArray.Length; i++)
            {
                var handle = _copyVelHandleArray[i];
                handle.Complete();
                _copyVelHandleArray[i] = handle;
            }

            // Complete all copy debug jobs before arrays are invalidated
            for (int i = 0; i < _copyDebugHandleArray.Length; i++)
            {
                var handle = _copyDebugHandleArray[i];
                handle.Complete();
                _copyDebugHandleArray[i] = handle;
            }
            
            _boidGroups.Clear();
            EntityManager.GetAllUniqueSharedComponentData(_boidGroups);

            // Schedule copy jobs for groups which data can be read
            for (int i = 0; i < _boidGroups.Count; i++)
            {
                _boidQuery.ResetFilter();
                
                BoidGroupDataCmp groupData = _boidGroups[i];
                _boidQuery.AddSharedComponentFilter(groupData);
                if(_boidQuery.IsEmpty)
                    continue;

                // Copy positions
                int groupId = groupData.groupId;
                if (_gpuDataManager.PosReadSyncBuffer.CanGetDataForGroup(groupId))
                {
                    var posArrayFromGpu = _gpuDataManager.PosReadSyncBuffer.GetDataForGroup(groupId);
                    var copyPosHandle = Entities
                        .WithName("CopyPosDataFromGpu")
                        .WithSharedComponentFilter(groupData)
                        .WithReadOnly(posArrayFromGpu)
                        .WithNone<NotSimulatedTagCmp>()
                        .WithAll<BoidTagCmp>()
                        .ForEach((int entityInQueryIndex, ref Translation pos) =>
                        {
                            pos.Value = posArrayFromGpu[entityInQueryIndex];
                        })
                        .ScheduleParallel(Dependency);

                    _copyPosHandleArray[groupId] = copyPosHandle;
                    Dependency = copyPosHandle;
                }
                
                // Copy velocities
                if (_gpuDataManager.VelReadSyncBuffer.CanGetDataForGroup(groupId))
                {
                    var velArrayFromGpu = _gpuDataManager.VelReadSyncBuffer.GetDataForGroup(groupId);
                    var copyVelHandle = Entities
                        .WithName("CopyVelDataFromGpu")
                        .WithSharedComponentFilter(groupData)
                        .WithReadOnly(velArrayFromGpu)
                        .WithNone<NotSimulatedTagCmp>()
                        .WithAll<BoidTagCmp>()
                        .ForEach((int entityInQueryIndex, ref VelocityCmp vel) =>
                        {
                            vel.Value = velArrayFromGpu[entityInQueryIndex];
                        })
                        .ScheduleParallel(Dependency);

                    _copyVelHandleArray[groupId] = copyVelHandle;
                    Dependency = copyVelHandle;
                }

                // Copy debug data
                if (_gpuDataManager.DebugReadSyncBuffer.CanGetDataForGroup(groupId))
                {
                    var debugArrayFromGpu = _gpuDataManager.DebugReadSyncBuffer.GetDataForGroup(groupId);
                    var copyDebugHandle = Entities
                        .WithName("CopyDebugDataFromGpu")
                        .WithSharedComponentFilter(groupData)
                        .WithReadOnly(debugArrayFromGpu)
                        .WithNone<NotSimulatedTagCmp>()
                        .WithAll<BoidTagCmp>()
                        .ForEach((int entityInQueryIndex, ref GpuDebugDataCmp debugData) =>
                        {
                            debugData.Value = debugArrayFromGpu[entityInQueryIndex];
                        })
                        .ScheduleParallel(Dependency);

                    _copyDebugHandleArray[groupId] = copyDebugHandle;
                    Dependency = copyDebugHandle;
                }
            }
        }
    }
    
    [ExecuteAlways]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class CopyDataFromGpuStartFrameSystem : CopyDataFromGpuSystem { }
    
    [ExecuteAlways]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class CopyDataFromGpuEndFrameSystem : CopyDataFromGpuSystem { }
}