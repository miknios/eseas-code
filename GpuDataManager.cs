using System.Collections.Generic;
using GPUInstancer;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace BoidECS.PerFishSimulationAlgorithm
{
    public class GpuDataManager : Singleton<GpuDataManager>
    {
        public const string TimeVariationBufferName = "animDataBuffer";

        public static int float2Stride = 2 * sizeof(float);
        public static int float3Stride = 3 * sizeof(float);
        public static int float4Stride = 4 * sizeof(float);

        private SpeciesPrefabManagerAsset _speciesPrefabManagerAsset;
        private GPUInstancerPrefabManager _gpuiPrefabManager;

        private int _groupsRegisteredCount;
        private List<int> _groupDataBufferStartIdx;
        private List<int> _boidsInGroupCount;
        private ComputeBuffer _boidPosBuffer;
        private ComputeBuffer _boidVelBuffer;
        private ComputeBuffer _obstacleAvoidanceBuffer;
        private ComputeBuffer _debugBuffer;
        private ComputeBuffer[] _gpuiBufferForSpeciesId;
        private ComputeBuffer[] _timeVariationBufferForSpeciesId;
        private ComputeBuffer _dynamicObstacleBuffer;

        private ReadSyncBuffer<float3> _posReadSyncBuffer;
        private ReadSyncBuffer<float3> _velReadSyncBuffer;
        private ReadSyncBuffer<GpuDebugData> _debugReadSyncBuffer;

        public ComputeBuffer BoidPosBuffer => _boidPosBuffer;
        public ComputeBuffer BoidVelBuffer => _boidVelBuffer;
        public ComputeBuffer ObstacleAvoidanceBuffer => _obstacleAvoidanceBuffer;
        public ComputeBuffer DebugBuffer => _debugBuffer;
        public ReadSyncBuffer<float3> PosReadSyncBuffer => _posReadSyncBuffer;
        public ReadSyncBuffer<float3> VelReadSyncBuffer => _velReadSyncBuffer;
        public ReadSyncBuffer<GpuDebugData> DebugReadSyncBuffer => _debugReadSyncBuffer;
        public ComputeBuffer DynamicObstacleBuffer => _dynamicObstacleBuffer;

        private void Awake()
        {
            // Init references
            _speciesPrefabManagerAsset = GlobalAssetHolder.Instance.SpeciesPrefabManagerAsset;
            _gpuiPrefabManager = GlobalAssetHolder.Instance.GPUInstancerPrefabManager;
            var spawnDataManager = FindObjectOfType<BoidSpawnManager>();

            // Init manager data
            _boidPosBuffer = new ComputeBuffer(spawnDataManager.AllBoidCount, float3Stride);
            _boidVelBuffer = new ComputeBuffer(spawnDataManager.AllBoidCount, float3Stride);
            _obstacleAvoidanceBuffer = new ComputeBuffer(spawnDataManager.AllBoidCount, float3Stride);

            _groupsRegisteredCount = 0;
            _boidsInGroupCount = new List<int>(spawnDataManager.AllGroupCount);
            _groupDataBufferStartIdx = new List<int>(spawnDataManager.AllGroupCount);

            _posReadSyncBuffer =
                new ReadSyncBuffer<float3>(this, _boidPosBuffer, spawnDataManager.AllGroupCount, float3Stride);
            _velReadSyncBuffer =
                new ReadSyncBuffer<float3>(this, _boidPosBuffer, spawnDataManager.AllGroupCount, float3Stride);

            unsafe
            {
                _debugBuffer = new ComputeBuffer(spawnDataManager.AllBoidCount, UnsafeUtility.SizeOf<GpuDebugData>());
                _debugReadSyncBuffer = new ReadSyncBuffer<GpuDebugData>(this, _debugBuffer, 
                    spawnDataManager.AllGroupCount, UnsafeUtility.SizeOf<GpuDebugData>());
            }

            // Init GPUI data
            int speciesCount = _speciesPrefabManagerAsset.GetSpeciesCount;
            _gpuiBufferForSpeciesId = new ComputeBuffer[speciesCount];
            _timeVariationBufferForSpeciesId = new ComputeBuffer[speciesCount];
            for (int speciesId = 0; speciesId < speciesCount; speciesId++)
            {
                int spawnCountForSpecies = spawnDataManager.SpawnCountForSpeciesId[speciesId];
                var gpuiPrototype = _speciesPrefabManagerAsset.GetPrefabPrototypeForSpeciesId(speciesId);
                GPUInstancerAPI.InitializePrototype(_gpuiPrefabManager, gpuiPrototype,
                    spawnCountForSpecies);

                var gpuiBuffer = GPUInstancerAPI.GetTransformDataBuffer(_gpuiPrefabManager, gpuiPrototype);
                _gpuiBufferForSpeciesId[speciesId] = gpuiBuffer;

                // Initialize instanced time data
                float2[] accValueArray = new float2[spawnCountForSpecies];
                for (int i = 0; i < spawnCountForSpecies; i++)
                {
                    accValueArray[i] = new float2(1, Random.Range(0, 6.28f));
                }
                
                var variationData = GPUInstancerAPI.DefineAndAddVariationFromArray(_gpuiPrefabManager, gpuiPrototype,
                    TimeVariationBufferName, accValueArray);

                _timeVariationBufferForSpeciesId[speciesId] = variationData.variationBuffer;
            }

            _dynamicObstacleBuffer = new ComputeBuffer(PlayerBallShooter.MaxBalls + 1, float3Stride);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _boidPosBuffer.Dispose();
            _boidVelBuffer.Dispose();
            _obstacleAvoidanceBuffer.Dispose();
            _dynamicObstacleBuffer.Dispose();
            _debugBuffer.Dispose();
            for (int i = 0; i < _speciesPrefabManagerAsset.GetSpeciesCount; i++)
            {
                _gpuiBufferForSpeciesId[i].Dispose();
                _timeVariationBufferForSpeciesId[i].Dispose();
            }
        }

        private void Update()
        {
            // Unlock reading if didn't get data before it's invalidated
            _posReadSyncBuffer.UnlockInvalidatedData();
            _velReadSyncBuffer.UnlockInvalidatedData();
            _debugReadSyncBuffer.UnlockInvalidatedData();
        }

        public int RegisterGroup(int boidInGroupCount)
        {
            _boidsInGroupCount.Add(boidInGroupCount);

            int startIdx = _groupsRegisteredCount == 0
                ? 0
                : _groupDataBufferStartIdx[_groupsRegisteredCount - 1] + _boidsInGroupCount[_groupsRegisteredCount - 1];
            _groupDataBufferStartIdx.Add(startIdx);

            return _groupsRegisteredCount++;
        }

        public int GetBoidsInGroupCount(int groupId)
        {
            return _boidsInGroupCount[groupId];
        }

        public void UpdateGroupPositions(int groupId, NativeArray<float3> posArray)
        {
            int groupStartIdx = _groupDataBufferStartIdx[groupId];
            int boidsInGroupCount = _boidsInGroupCount[groupId];

            _boidPosBuffer.SetData(posArray, 0, groupStartIdx, boidsInGroupCount);
        }

        public void UpdateGroupVelocities(int groupId, NativeArray<float3> velArray)
        {
            int groupStartIdx = _groupDataBufferStartIdx[groupId];
            int boidsInGroupCount = _boidsInGroupCount[groupId];

            _boidVelBuffer.SetData(velArray, 0, groupStartIdx, boidsInGroupCount);
        }

        public void UpdateGroupObstacleAvoidanceData(int groupId, NativeArray<float3> obstacleAvoidanceArray)
        {
            int groupStartIdx = _groupDataBufferStartIdx[groupId];
            int boidsInGroupCount = _boidsInGroupCount[groupId];

            _obstacleAvoidanceBuffer.SetData(obstacleAvoidanceArray, 0, groupStartIdx, boidsInGroupCount);
        }

        public void UpdateGroupDebugData(int groupId, NativeArray<GpuDebugData> groupDebugArray)
        {
            int groupStartIdx = _groupDataBufferStartIdx[groupId];
            int boidsInGroupCount = _boidsInGroupCount[groupId];

            _debugBuffer.SetData(groupDebugArray, 0, groupStartIdx, boidsInGroupCount);
        }

        public ComputeBuffer GetGPUITransformBufferForSpeciesId(int speciesId)
        {
            return _gpuiBufferForSpeciesId[speciesId];
        }

        public ComputeBuffer GetTimeVariationBufferForSpeciesId(int speciesId)
        {
            return _timeVariationBufferForSpeciesId[speciesId];
        }

        public int GetGroupConstantBufferStartIdx(int groupId)
        {
            return _groupDataBufferStartIdx[groupId];
        }


        public class ReadSyncBuffer<T> where T : struct
        {
            private GpuDataManager _dataManager;
            private ComputeBuffer _buffer;
            private AsyncGPUReadbackRequest[] _groupRequestHandleArray;
            private ArrayReadSync[] _groupReadSync;
            private int _stride;
            
            public ReadSyncBuffer(GpuDataManager dataManager, ComputeBuffer buffer, int maxGroupCount, int stride)
            {
                _dataManager = dataManager;
                _buffer = buffer;
                _groupRequestHandleArray = new AsyncGPUReadbackRequest[maxGroupCount];
                _groupReadSync = new ArrayReadSync[maxGroupCount];
                _stride = stride;
            }

            public bool CanRequestDataForGroup(int groupId)
            {
                var groupSync = _groupReadSync[groupId];
                return groupSync.CanRequestNextData;
            }

            public void StartDataRequestForGroup(int groupId)
            {
                if (!CanRequestDataForGroup(groupId))
                {
                    Debug.LogError("Trying to start data request when it's not possible!");
                    return;
                }

                // Add request to sync
                ArrayReadSync groupSync = _groupReadSync[groupId];
                groupSync.AddRequest();
                _groupReadSync[groupId] = groupSync;

                // Start request and return handle
                int groupStartIdx = _dataManager._groupDataBufferStartIdx[groupId];
                int groupCount = _dataManager._boidsInGroupCount[groupId];

                _groupRequestHandleArray[groupId] =
                    AsyncGPUReadback.Request(_buffer, _stride * groupCount,
                        _stride * groupStartIdx);
            }

            public bool TryRequestDataForGroup(int groupId)
            {
                if (CanRequestDataForGroup(groupId))
                {
                    StartDataRequestForGroup(groupId);
                    return true;
                }

                return false;
            }

            public bool CanGetDataForGroup(int groupId)
            {
                // Check sync
                ArrayReadSync readSync = _groupReadSync[groupId];
                if (!readSync.CanReadNewData)
                    return false;

                var requestHandle = _groupRequestHandleArray[groupId];
                return !requestHandle.hasError && requestHandle.done;
            }

            public NativeArray<T> GetDataForGroup(int groupId)
            {
                if (!CanGetDataForGroup(groupId))
                {
                    Debug.LogError("Trying to get data when it's not possible!");
                }

                // Add read
                ArrayReadSync readSync = _groupReadSync[groupId];
                readSync.AddRead();
                _groupReadSync[groupId] = readSync;

                // Return results
                return _groupRequestHandleArray[groupId].GetData<T>();
            }

            public void UnlockInvalidatedData()
            {
                for (int groupId = 0; groupId < _groupReadSync.Length; groupId++)
                {
                    var readSync = _groupReadSync[groupId];
                    if (readSync.CanReadNewData &&
                        _groupRequestHandleArray[groupId].hasError)
                    {
                        readSync.Reset();
                        _groupReadSync[groupId] = readSync;
                    }
                }
            }

            public int GetLastReadDataVerForGroup(int groupId)
            {
                return _groupReadSync[groupId].LastReadDataVer;
            }

            private struct ArrayReadSync
            {
                private int lastReadDataVer;
                private int requests;
                private int reads;

                public bool CanRequestNextData => requests == reads;
                public bool CanReadNewData => requests - reads == 1;
                public int LastReadDataVer => lastReadDataVer;

                public void AddRead()
                {
                    reads++;
                    lastReadDataVer++;
                }

                public void AddRequest()
                {
                    requests++;
                }

                public void Reset()
                {
                    requests = 0;
                    reads = 0;
                }
            }
        }
    }
}