using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public class RandomSpawnZone : SpawnZone
    {
        public float radius;

        public void SetMaxBoidCount(int value)
        {
            maxBoidCount = value;
        }
        
        public override float3 GetRandomPos()
        {
            float3 pos = transform.position + Random.insideUnitSphere * radius;
            
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float terrainHeight = terrain.SampleHeight(new Vector3(pos.x, 0, pos.y)) +
                                      terrain.GetPosition().y + 2;

                if (pos.y < terrainHeight)
                    pos.y = Random.Range(terrainHeight, transform.position.y + radius);
            }

            return pos;
        }
    }
}