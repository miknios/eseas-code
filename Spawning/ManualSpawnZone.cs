using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public class ManualSpawnZone : SpawnZone
    {
        public override float3 GetRandomPos()
        {
            Terrain terrain = Terrain.activeTerrain;
            
            float3 pos = transform.TransformPoint(new float3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.5f, 0.5f)));
            
            if (terrain != null)
            {
                float terrainHeight = terrain.SampleHeight(new Vector3(pos.x, 0, pos.y)) +
                                      terrain.GetPosition().y + 2;

                if (transform.TransformPoint(Vector3.up * -0.5f).y < terrainHeight)
                    pos.y = Random.Range(terrainHeight, transform.TransformPoint(Vector3.up * 0.5f).y);
            }

            return pos;
        }
    }
}