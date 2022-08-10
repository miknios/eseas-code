using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [ExecuteAlways]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class UpdateDynamicObstaclesSystem : SystemBase
    {
        private PlayerBallShooter _playerBallShooter;
        private GpuDataManager _gpuDataManager;
        private NativeArray<float3> _dynamicObstacleData;

        protected override void OnCreate()
        {
            _playerBallShooter = GlobalAssetHolder.Instance.PlayerBallShooter;
            _gpuDataManager = GpuDataManager.Instance;
            _dynamicObstacleData = new NativeArray<float3>(PlayerBallShooter.MaxBalls + 1, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            _dynamicObstacleData.Dispose();
        }

        protected override void OnUpdate()
        {
            if(_playerBallShooter == null || 
               _gpuDataManager == null || 
               _gpuDataManager.DynamicObstacleBuffer == null)
                return;
            
            _dynamicObstacleData[0] = _playerBallShooter.transform.position;
            for (int i = 0; i < _playerBallShooter.ActiveBalls.Count; i++)
            {
                _dynamicObstacleData[i + 1] = _playerBallShooter.ActiveBalls[i].position;
            }

            _gpuDataManager.DynamicObstacleBuffer.SetData(_dynamicObstacleData, 0, 0, _playerBallShooter.ActiveBalls.Count + 1);
        }
    }
}