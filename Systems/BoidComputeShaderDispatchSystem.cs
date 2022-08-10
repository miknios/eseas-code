#if BOIDS_CS

using System.Collections.Generic;
using GPUInstancer;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [ExecuteAlways]
    public class BoidComputeShaderDispatchSystem : SystemBase
    {
        private GPUInstancerPrefabManager _gpuiManager;
        private SpeciesPrefabManagerAsset _speciesPrefabManager;
        private GpuDataManager _gpuDataManager;
        private BoidSettings _boidSettings;
        private PlayerBallShooter _playerBallShooter;

        private EntityQuery _boidQuery;
        private List<BoidGroupDataCmp> _boidGroups;
        private int[] _nextSpeciesGpuiPosBufferStartIdx;

        private ComputeShader _boidComputeShader;
        private ComputeBuffer _gpuiPosBuffer;

        protected override void OnCreate()
        {
            // Init references
            _gpuiManager = GlobalAssetHolder.Instance.GPUInstancerPrefabManager;
            _boidComputeShader = GlobalAssetHolder.Instance.BoidComputeShader;
            _speciesPrefabManager = GlobalAssetHolder.Instance.SpeciesPrefabManagerAsset;
            _gpuDataManager = GpuDataManager.Instance;
            _boidSettings = GlobalAssetHolder.Instance.BoidSettings;
            _playerBallShooter = GlobalAssetHolder.Instance.PlayerBallShooter;

            // Init data
            int maxGroupCount = GlobalAssetHolder.Instance.BoidSpawnManager.AllGroupCount;
            _boidGroups = new List<BoidGroupDataCmp>(maxGroupCount);
            _nextSpeciesGpuiPosBufferStartIdx = new int[_speciesPrefabManager.GetSpeciesCount];
            _boidQuery = GetEntityQuery(EntityQueryHelper.simulatedBoidComponentTypes);
        }

        protected override void OnUpdate()
        {
            // Clear buffers
            _boidGroups.Clear();
            for (int i = 0; i < _nextSpeciesGpuiPosBufferStartIdx.Length; i++)
                _nextSpeciesGpuiPosBufferStartIdx[i] = 0;

            // Fill list with all possible values of BoidGroupCmp
            EntityManager.GetAllUniqueSharedComponentData(_boidGroups);

            float dt = Time.DeltaTime;

            for (int i = 0; i < _boidGroups.Count; i++)
            {
                // Clear previous query filters, because query object is reused
                _boidQuery.ResetFilter();

                // Get group data and set it as a query filter
                var groupDataCmp = _boidGroups[i];
                _boidQuery.AddSharedComponentFilter(groupDataCmp);

                // Skip if there is no entity matching this group
                if (_boidQuery.IsEmpty)
                    continue;

                int boidInGroupCount = _gpuDataManager.GetBoidsInGroupCount(groupDataCmp.groupId);
                SetComputeShaderData(dt, boidInGroupCount, groupDataCmp);
                _boidComputeShader.Dispatch(0,
                    Mathf.CeilToInt(boidInGroupCount / GPUInstancerConstants.COMPUTE_SHADER_THREAD_COUNT), 1, 1);

                // Sum fish count within a species. This value is also a starting index for next group of this species
                _nextSpeciesGpuiPosBufferStartIdx[groupDataCmp.speciesId] += boidInGroupCount;

                // Request new data for processed group if previous data were acquired. We need this data, so we can calculate avoidance vectors with physics
                int groupId = groupDataCmp.groupId;
                _gpuDataManager.PosReadSyncBuffer.TryRequestDataForGroup(groupId);
                _gpuDataManager.VelReadSyncBuffer.TryRequestDataForGroup(groupId);

#if BOID_GPUDEBUG
                // Request new debug data if previous request is finished
                _gpuDataManager.DebugReadSyncBuffer.TryRequestDataForGroup(groupId);
#endif
            }

            // Pass instance count to render for each species
            for (int speciesId = 0; speciesId < _speciesPrefabManager.GetSpeciesCount; speciesId++)
            {
                var prototype = _speciesPrefabManager.GetPrefabPrototypeForSpeciesId(speciesId);
                int instanceCount = _nextSpeciesGpuiPosBufferStartIdx[speciesId];

                GPUInstancerAPI.SetInstanceCount(_gpuiManager, prototype, instanceCount);
            }
        }

        private void SetComputeShaderData(float dt, int groupCount, in BoidGroupDataCmp groupDataCmp)
        {
            int groupId = groupDataCmp.groupId;
            int speciesId = groupDataCmp.speciesId;

            int gpuiPosBufferStartIdx = _nextSpeciesGpuiPosBufferStartIdx[speciesId];
            int constBufferStartIdx = _gpuDataManager.GetGroupConstantBufferStartIdx(groupId);

            _boidComputeShader.SetBuffer(0, "gpuiPosBuffer",
                _gpuDataManager.GetGPUITransformBufferForSpeciesId(speciesId));
            _boidComputeShader.SetBuffer(0, "animDataBuffer",
                _gpuDataManager.GetTimeVariationBufferForSpeciesId(speciesId));
            _boidComputeShader.SetBuffer(0, "boidPosBuffer", _gpuDataManager.BoidPosBuffer);
            _boidComputeShader.SetBuffer(0, "boidVelBuffer", _gpuDataManager.BoidVelBuffer);
            _boidComputeShader.SetBuffer(0, "obstacleAvoidanceVec", _gpuDataManager.ObstacleAvoidanceBuffer);
            _boidComputeShader.SetBuffer(0, "debugDataBuffer", _gpuDataManager.DebugBuffer);
            _boidComputeShader.SetBuffer(0, "dynamicObstaclePosBuffer", _gpuDataManager.DynamicObstacleBuffer);

            _boidComputeShader.SetFloat("dt", dt);
            _boidComputeShader.SetInt("constantBufferStartIdx", constBufferStartIdx);
            _boidComputeShader.SetInt("gpuiBufferStartIdx", gpuiPosBufferStartIdx);
            _boidComputeShader.SetInt("groupCount", groupCount);

            var behavConfig = groupDataCmp.behaviourConfig;
            _boidComputeShader.SetFloat("visionAngleDot",
                math.cos(math.radians(behavConfig.visibilityData.VisionAngle)));
            _boidComputeShader.SetFloat("visionDistance", behavConfig.visibilityData.Distance);
            _boidComputeShader.SetInt("maxVisibleBoidsCount", behavConfig.visibleBoidMax);
            _boidComputeShader.SetFloat("speed", behavConfig.speed);
            _boidComputeShader.SetFloat("maxSpeed", _boidSettings.MaxSpeed);
            _boidComputeShader.SetFloat("maxCatchupSpeedBonusPercent", _boidSettings.MaxCatchupSpeedBonusPercent);
            _boidComputeShader.SetFloat("maxDynamicObstacleAvoidanceSpeedBonusPercent",
                _boidSettings.MaxDynamicObstacleAvoidanceSpeedBonusPercent);
            _boidComputeShader.SetFloat("speedAlignmentLerp", _boidSettings.SpeedAlignmentLerp);
            _boidComputeShader.SetFloat("separationTreshold", behavConfig.separationTreshold);
            _boidComputeShader.SetFloat("boundDistTreshold", _boidSettings.BoundAvoidanceTreshold);
            _boidComputeShader.SetFloat("velLerpValue", _boidSettings.VelocityLerpValue);
            _boidComputeShader.SetFloat("flappingLerpIncrease", _boidSettings.FlappingLerpIncrease);
            _boidComputeShader.SetFloat("flappingLerpDecrease", _boidSettings.FlappingLerpDecrease);
            _boidComputeShader.SetFloat("flappingTimescaleMultiplier", _boidSettings.FlappingTimescaleMultiplier);
            _boidComputeShader.SetFloat("flappingMultiplierSpeedRef", _boidSettings.FlappingMultiplierSpeedRef);
            _boidComputeShader.SetFloat("flappingMultiplierAngleRef", _boidSettings.FlappingMultiplierAngleRef);
            _boidComputeShader.SetFloat("speedMultiplierAngleRef", _boidSettings.SpeedMultiplierAngleRef);
            _boidComputeShader.SetFloat("minAngleSpeedMultiplier", _boidSettings.MinAngleSpeedMultiplier);

            _boidComputeShader.SetFloat("cohesionWeight", behavConfig.ruleWeightData.CohesionWeight);
            _boidComputeShader.SetFloat("separationWeight", behavConfig.ruleWeightData.SeparatonWeight);
            _boidComputeShader.SetFloat("alignmentWeight", behavConfig.ruleWeightData.AlignmentWeight);
            _boidComputeShader.SetFloat("boundAvoidanceWeight", _boidSettings.BoundAvoidanceWeight);
            _boidComputeShader.SetFloat("obstacleAvoidanceWeight", _boidSettings.ObstacleAvoidanceWeight);

            float3 modelScale = groupDataCmp.modelScale;
            _boidComputeShader.SetVector("scale", new Vector4(modelScale.x, modelScale.y, modelScale.z));

            Bounds bounds = groupDataCmp.CalculateBounds;
            _boidComputeShader.SetVector("boundsMin", new Vector4(bounds.min.x, bounds.min.y, bounds.min.z));
            _boidComputeShader.SetVector("boundsMax", new Vector4(bounds.max.x, bounds.max.y, bounds.max.z));

            _boidComputeShader.SetFloat("dynamicObstacleRadius", _boidSettings.DynamicObstacleAvoidanceRadius);
            _boidComputeShader.SetFloat("dynamicObstacleAvoidanceTreshold", _boidSettings.DynamicObstacleTreshold);
            _boidComputeShader.SetInt("dynamicObstacleCount", _playerBallShooter.ActiveBalls.Count + 1);
            _boidComputeShader.SetFloat("dynamicObstacleMaxLerpBonus", _boidSettings.DynamicObstacleMaxLerpBonus);
            _boidComputeShader.SetFloat("dynamicObstacleMinFrac", _boidSettings.DynamicObstacleMinFrac);
        }
    }
}


#endif