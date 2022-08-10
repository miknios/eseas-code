using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public struct BoidGroupDataCmp : ISharedComponentData, IEquatable<BoidGroupDataCmp>
    {
        public int groupId;
        public int speciesId;
        public SpeciesBehaviourConfig behaviourConfig;
        public float3 boundsCenter;
        public float3 boundsSize;
        public float3 modelScale;

        public Bounds CalculateBounds => new Bounds(boundsCenter, boundsSize);

        public bool Equals(BoidGroupDataCmp other)
        {
            return groupId == other.groupId && speciesId == other.speciesId;
        }

        public override bool Equals(object obj)
        {
            return obj is BoidGroupDataCmp other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (groupId * 397) ^ speciesId;
            }
        }
    }
}