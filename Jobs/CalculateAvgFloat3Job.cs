using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace BoidECS.PerFishSimulationAlgorithm
{
    [BurstCompile]
    public struct CalculateAvgFloat3Job : IJob
    {
        public NativeArray<float3> Input;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> Output;
        public int OutputIdx;
            
        public void Execute()
        {
            float3 output = float3.zero;
            for (int i = 0; i < Input.Length; i++)
            {
                output += Input[i];
            }
            output /= Input.Length;
                
            Output[OutputIdx] = output;
        }
    }
}