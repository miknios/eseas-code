using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class SimulateVisibleEntitiesSystem : SystemBase
    {
        private Transform _cameraTransform;
        private BoidSettings _boidSetting;

        private List<BoidGroupDataCmp> _speciesDataCmps;
        private EntityQuery _simulatedBoidQuery;
        private EntityQuery _notSimulatedBoidQuery;

        protected override void OnCreate()
        {
            _boidSetting = GlobalAssetHolder.Instance.BoidSettings;
            _cameraTransform = GameObject.FindObjectOfType<Camera>().transform;
            _speciesDataCmps = new List<BoidGroupDataCmp>(5);

            _simulatedBoidQuery = GetEntityQuery(ComponentType.ReadOnly<BoidTagCmp>(),
                ComponentType.ReadOnly<BoidGroupDataCmp>(), ComponentType.Exclude<NotSimulatedTagCmp>());

            _notSimulatedBoidQuery = GetEntityQuery(ComponentType.ReadOnly<BoidTagCmp>(),
                ComponentType.ReadOnly<BoidGroupDataCmp>(), ComponentType.ReadOnly<NotSimulatedTagCmp>());
        }

        protected override void OnUpdate()
        {
            EntityManager.GetAllUniqueSharedComponentData(_speciesDataCmps);

            float3 camPos = _cameraTransform.position;
            for (int i = 0; i < _speciesDataCmps.Count; i++)
            {
                var speciesData = _speciesDataCmps[i];
                var bounds = speciesData.CalculateBounds;

                if (bounds.SqrDistance(camPos) > _boidSetting.NoSimBoundsToCamSqrDist)
                {
                    _simulatedBoidQuery.SetSharedComponentFilter(speciesData);
                    EntityManager.AddComponent<NotSimulatedTagCmp>(_simulatedBoidQuery);
                    _simulatedBoidQuery.ResetFilter();
                }
                else
                {
                    _notSimulatedBoidQuery.SetSharedComponentFilter(speciesData);
                    EntityManager.RemoveComponent<NotSimulatedTagCmp>(_notSimulatedBoidQuery);
                    _notSimulatedBoidQuery.ResetFilter();
                }
            }

            _speciesDataCmps.Clear();
        }
    }
}