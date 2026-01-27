using SynchronizationPrimitives.Examples;

/// <summary>
/// <see href="https://learn.microsoft.com/en-us/dotnet/standard/threading/overview-of-synchronization-primitives"> Synchronization Primitives </see>
/// </summary>

Console.WriteLine("Демонстрация работы с примитивов синхпранизации .NET");

await InterlockedExample.Demo();
await SpinLockExample.Demo();
await MonitorExample.Demo();
await MutexExample.Demo();
await SemaphoreExample.Demo();
await ReaderWriterExample.Demo();
await EventExample.Demo();
await CountdownExample.Demo();
await BarrierExample.Demo();

Console.WriteLine("Сравнение производительности");

await RunPerformanceComparison(1_000_000);

/// <summary>
/// Метод сравнения производительности различных примитивов
/// </summary>
/// <returns></returns>
static async Task RunPerformanceComparison(int iterationCount)
{
    await InterlockedExample.PerformanceTest(iterationCount);
    await SpinLockExample.PerformanceTest(iterationCount);
    await MonitorExample.PerformanceTest(iterationCount);
    await ReaderWriterExample.PerformanceTest(iterationCount);
}