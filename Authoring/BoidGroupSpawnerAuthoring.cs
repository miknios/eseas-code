using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Drawing;
using Samples.Boids.Boids;
using Sirenix.OdinInspector;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;
using RangeInt = Samples.Boids.Boids.RangeInt;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public static class ListEx
    {
        public static T GetRandom<T>(this IList<T> list) => list[UnityEngine.Random.Range(0, list.Count)];
    }
    
    public class BoidGroupSpawnerAuthoring : MonoBehaviourGizmos, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
    {
        [SerializeField, InlineEditor, Required]
        private SpeciesBehaviourConfigAsset behaviourConfigAsset;

        [SerializeField] private int spawnCount;

        [SerializeField, HideIf(nameof(HasOverridenBounds))]
        private float3 movementBounds;

        [SerializeField] private Transform movementBoundsOverride;

        [SerializeField, BoxGroup("Random spawn zones")]
        private RangeInt randomSpawnZoneBoidCountRange;

        [SerializeField, BoxGroup("Random spawn zones")]
        private RangeFloat randomSpawnZoneRadiusRange;

        [SerializeField] private List<GameObject> randomSpawnZoneBoundParent;

        private bool HasOverridenBounds => movementBoundsOverride != null;

        public int SpawnCount => spawnCount;

        public int SpeciesId => behaviourConfigAsset.ID;

        public List<ManualSpawnZone> ManualSpawnZones =>
            GetComponentsInChildren<ManualSpawnZone>(false).ToList();

        public List<RandomSpawnZone> RandomSpawnZones =>
            GetComponentsInChildren<RandomSpawnZone>().ToList();

        public List<RandomSpawnZoneBound> GetRandomSpawnZoneBounds()
        {
            if (randomSpawnZoneBoundParent == null)
                return new List<RandomSpawnZoneBound>();
            return randomSpawnZoneBoundParent
                .SelectMany(e => e.GetComponentsInChildren<RandomSpawnZoneBound>())
                .ToList();
        }

        public MovementBoundsData GetMovementBoundsData()
        {
            if (movementBoundsOverride == null)
            {
                return new MovementBoundsData
                {
                    pos = transform.position,
                    size = movementBounds
                };
            }

            return new MovementBoundsData
            {
                pos = movementBoundsOverride.position,
                size = movementBoundsOverride.lossyScale
            };
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            if (!gameObject.activeSelf)
                return;

            referencedPrefabs.Add(behaviourConfigAsset.EntityPrefab);
        }

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (!gameObject.activeSelf)
                return;

            // TODO: should it be here?
            // RegenerateRandomSpawnZones();

            var prefabEntity = conversionSystem.GetPrimaryEntity(behaviourConfigAsset.EntityPrefab);

            var movementBoundsData = GetMovementBoundsData();

            dstManager.AddComponentData(entity, new BoidGroupSpawnerCmp
            {
                prefab = prefabEntity,
                speciesId = behaviourConfigAsset.ID,
                groupCount = spawnCount,
                centerPos = movementBoundsData.pos,
                movementBoundsSize = movementBoundsData.size,
                boidScale = behaviourConfigAsset.GPUIPrefab.transform.lossyScale,
                behaviourConfig = behaviourConfigAsset.Config
            });

            dstManager.AddComponentObject(entity, this);
        }

        public override void DrawGizmos()
        {
            if (behaviourConfigAsset == default)
                return;

            bool boundsSelected = GizmoContext.InSelection(this) ||
                                  movementBoundsOverride != null && GizmoContext.InSelection(movementBoundsOverride);
            float boundsLineWidth = boundsSelected ? 1.5f : 0.75f;
            using (Draw.WithLineWidth(boundsLineWidth))
            {
                var movementBoundsData = GetMovementBoundsData();
                Draw.WireBox(movementBoundsData.pos, movementBoundsData.size, Color.green);
            }

            if (!Application.isPlaying)
            {
                foreach (var spawnZone in ManualSpawnZones)
                {
                    float spawnZoneLineWidth = GizmoContext.InSelection(spawnZone) ? 1.5f : 1f;
                    using (Draw.WithLineWidth(spawnZoneLineWidth))
                    {
                        Draw.WireBox(spawnZone.transform.position, spawnZone.transform.rotation,
                            spawnZone.transform.lossyScale,
                            behaviourConfigAsset.Color);
                        Draw.Arrow(spawnZone.transform.position,
                            spawnZone.transform.position + spawnZone.transform.forward * 2.5f,
                            Color.blue);
                    }
                }

                // Draw random spawn zones bounds 
                foreach (var spawnZoneBound in GetRandomSpawnZoneBounds())
                {
                    float spawnZoneLineWidth = GizmoContext.InSelection(spawnZoneBound) ? 1.5f : 1f;

                    using (Draw.WithLineWidth(spawnZoneLineWidth))
                    {
                        Draw.WireBox(spawnZoneBound.transform.position, spawnZoneBound.transform.rotation,
                            spawnZoneBound.transform.lossyScale,
                            Color.cyan);
                    }
                }

                // Draw random spawn zones
                foreach (var spawnZone in RandomSpawnZones)
                {
                    if (!GizmoContext.InSelection(this) || !GizmoContext.InSelection(spawnZone))
                        continue;

                    float spawnZoneLineWidth = GizmoContext.InSelection(spawnZone) ? 1.5f : 1f;

                    using (Draw.WithLineWidth(spawnZoneLineWidth))
                    {
                        Draw.WireSphere(spawnZone.transform.position, spawnZone.radius,
                            behaviourConfigAsset.Color);
                        Draw.Arrow(spawnZone.transform.position,
                            spawnZone.transform.position + spawnZone.transform.forward * 2.5f,
                            Color.blue);
                    }
                }
            }
        }

        public struct MovementBoundsData
        {
            public float3 pos;
            public float3 size;
        }

        public SpawnZone GetRandomSpawnZone()
        {
            if (ManualSpawnZones.Any(e => !e.IsFull))
            {
                return ManualSpawnZones
                    .Where(e => !e.IsFull)
                    .ToList()
                    .GetRandom();
            }

            // TODO: add dupochron ze jak nie ma zadnych niepelnych spawn zonow to generowanie nowego, i zwracanie
            return RandomSpawnZones
                .Where(e => !e.IsFull)
                .ToList()
                .GetRandom();
        }

        [Button]
        public void RegenerateRandomSpawnZones()
        {
            // Destroy old spawn zones
            var oldSpawnZones = RandomSpawnZones;
            for (int i = oldSpawnZones.Count - 1; i >= 0; i--)
            {
                if (!Application.isPlaying)
                    DestroyImmediate(oldSpawnZones[i].gameObject);
                else
                    Destroy(oldSpawnZones[i].gameObject);

#if UNITY_EDITOR
                EditorUtility.SetDirty(gameObject);
#endif
            }

            if (randomSpawnZoneBoidCountRange.max <= 0)
            {
                Debug.LogError("Random spawn zone boid count range max <= 0!!!");
                return;
            }

            // Generate new spawn zones
            int boidCountFromManual = ManualSpawnZones.Sum(e => e.MaxBoidCount);
            int boidToGenerate = spawnCount - boidCountFromManual;
            while (boidToGenerate > 0)
            {
                var bounds = GetRandomSpawnZoneBounds().GetRandom();
                GameObject newSpawnZone = new GameObject("Random Spawn Zone");
                var randomSpawnZone = newSpawnZone.AddComponent<RandomSpawnZone>();

                Vector3 pos = bounds.GetRandomPos();
                randomSpawnZone.transform.position = pos;
                randomSpawnZone.transform.SetParent(transform);
                randomSpawnZone.transform.rotation = Random.rotation;

                float t = Random.value;
                randomSpawnZone.radius = randomSpawnZoneRadiusRange.Lerp(t);

                int boidCount = randomSpawnZoneBoidCountRange.Lerp(t);
                randomSpawnZone.SetMaxBoidCount(boidCount);
                boidToGenerate -= boidCount;
            }
        }
    }
}