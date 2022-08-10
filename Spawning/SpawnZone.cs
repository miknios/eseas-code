using System;
using Unity.Mathematics;
using UnityEngine;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public abstract class SpawnZone : MonoBehaviour
    {
        [SerializeField] protected int maxBoidCount;

        protected int boidSpawned;

        public int MaxBoidCount => maxBoidCount;
        public bool IsFull => boidSpawned >= maxBoidCount;

        private void Awake()
        {
            boidSpawned = 0;
        }

        public abstract float3 GetRandomPos();

        public void RegisterSpawn()
        {
            boidSpawned++;
        }
    }
}