using System;
using Samples.Boids.Boids;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [CreateAssetMenu(fileName = "BoidSettings", menuName = "eseas/PerFishSim/BoidSettings", order = 0)]
    public class BoidSettings : ScriptableObject
    {
        private const string optimizations_group_name = "Optimizations";
        private const string animation_group_name = "Animation";
        private const string movement_group_name = "Movement";

        [SerializeField] private BoidDebugSettings debugSettings;
        [SerializeField] private ObstacleAvoidanceData obstacleAvoidanceData;

        
        // Movement fields
        [SerializeField, BoxGroup(movement_group_name)]
        private float velocityLerpValue;

        [SerializeField, BoxGroup(movement_group_name)]
        private float speedMultiplierAngleRef;

        [SerializeField, BoxGroup(movement_group_name)]
        private float minAngleSpeedMultiplier;

        [SerializeField, BoxGroup(movement_group_name)]
        private float boundAvoidanceWeight;

        [SerializeField, BoxGroup(movement_group_name)]
        private float boundAvoidanceTreshold;

        [SerializeField, BoxGroup(movement_group_name)]
        private float obstacleAvoidanceWeight;

        [SerializeField, BoxGroup(movement_group_name)]
        private float dynamicObstacleAvoidanceTreshold;

        [SerializeField, BoxGroup(movement_group_name)]
        private float dynamicObstacleAvoidanceRadius;

        [SerializeField, BoxGroup(movement_group_name)]
        private float dynamicObstacleMaxLerpBonus;

        [SerializeField, BoxGroup(movement_group_name)]
        private float dynamicObstacleMinFrac;

        [SerializeField, BoxGroup(movement_group_name)]
        private float boidCollisionCastRadius;

        [SerializeField, BoxGroup(movement_group_name)]
        private float maxCatchupSpeedBonusPercent;

        [SerializeField, BoxGroup(movement_group_name)]
        private float maxDynamicObstacleAvoidanceSpeedBonusPercent;

        [SerializeField, BoxGroup(movement_group_name)]
        private float maxSpeed;

        [SerializeField, BoxGroup(movement_group_name)]
        private float speedAlignmentLerp;

        
        // Animation fields
        [SerializeField, BoxGroup(animation_group_name)]
        private float flappingLerpIncrease;

        [SerializeField, BoxGroup(animation_group_name)]
        private float flappingLerpDecrease;

        [SerializeField, BoxGroup(animation_group_name)]
        private float flappingTimescaleMultiplier;

        [SerializeField, BoxGroup(animation_group_name)]
        private float flappingMultiplierSpeedRef;

        [SerializeField, BoxGroup(animation_group_name)]
        private float flappingMultiplierAngleRef;


        // Optimization fields
        [SerializeField, BoxGroup(optimizations_group_name)]
        private float noSimBoundsToCamDist;

        [SerializeField, BoxGroup(optimizations_group_name)]
        private int gatherVisibleBoidsJobBatchCount;

        [SerializeField, BoxGroup(optimizations_group_name)]
        private int obstacleAvoidanceDirectionsToCheck;

        public int GatherVisibleBoidsJobBatchCount => gatherVisibleBoidsJobBatchCount;
        public BoidDebugSettings DebugSettings => debugSettings;
        public ObstacleAvoidanceData ObstacleAvoidanceData => obstacleAvoidanceData;
        public float NoSimBoundsToCamSqrDist => noSimBoundsToCamDist * noSimBoundsToCamDist;
        public float VelocityLerpValue => velocityLerpValue;
        public float FlappingLerpIncrease => flappingLerpIncrease;
        public float FlappingLerpDecrease => flappingLerpDecrease;
        public int ObstacleAvoidanceDirectionsCount => obstacleAvoidanceDirectionsToCheck;
        public float BoundAvoidanceWeight => boundAvoidanceWeight;
        public float ObstacleAvoidanceWeight => obstacleAvoidanceWeight;
        public float BoundAvoidanceTreshold => boundAvoidanceTreshold;
        public float DynamicObstacleTreshold => dynamicObstacleAvoidanceTreshold;
        public float DynamicObstacleAvoidanceRadius => dynamicObstacleAvoidanceRadius;
        public float DynamicObstacleMaxLerpBonus => dynamicObstacleMaxLerpBonus;
        public float DynamicObstacleMinFrac => dynamicObstacleMinFrac;
        public float BoidCollisionCastRadius => boidCollisionCastRadius;
        public float MaxCatchupSpeedBonusPercent => maxCatchupSpeedBonusPercent;
        public float SpeedAlignmentLerp => speedAlignmentLerp;
        public float FlappingTimescaleMultiplier => flappingTimescaleMultiplier;
        public float FlappingMultiplierSpeedRef => flappingMultiplierSpeedRef;
        public float FlappingMultiplierAngleRef => flappingMultiplierAngleRef;
        public float SpeedMultiplierAngleRef => speedMultiplierAngleRef;
        public float MinAngleSpeedMultiplier => minAngleSpeedMultiplier;
        public float MaxDynamicObstacleAvoidanceSpeedBonusPercent => maxDynamicObstacleAvoidanceSpeedBonusPercent;
        public float MaxSpeed => maxSpeed;

        public struct BoidDebugSettings
        {
            public bool showGizmos;
            public float ruleArrowScale;
            public Color boundAvoidanceColor;
            public Color obstacleAvoidanceColor;
            public Color separationColor;
            public Color cohesionColor;
            public Color alignmentColor;
            public float boidPosSize;
            public Color boidPosColor;
            public float labelSize;
        }
    }
}