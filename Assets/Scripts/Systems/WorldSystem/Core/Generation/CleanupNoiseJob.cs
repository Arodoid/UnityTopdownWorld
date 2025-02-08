using Unity.Jobs;
using Unity.Collections;

public struct CleanupNoiseJob : IJob
{
    [DeallocateOnJobCompletion]
    public NativeArray<float> NoiseValues3D;
    [DeallocateOnJobCompletion]
    public NativeArray<float> HeightOffsets;
    [DeallocateOnJobCompletion]
    public NativeArray<float> TemperatureMap;
    [DeallocateOnJobCompletion]
    public NativeArray<float> HumidityMap;

    public void Execute() { }
} 