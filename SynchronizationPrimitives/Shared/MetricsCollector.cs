using System;
using System.Collections.Generic;
using System.Text;

namespace SynchronizationPrimitives.Shared
{
    /// <summary>
    /// Сбор метрик для сравнения
    /// </summary>
    public static class MetricsCollector
    {
        private static readonly System.Diagnostics.Stopwatch _stopwatch = new();
        private static long _totalOperations;
        private static long _totalTimeMs;

        public static void StartMeasure()
        {
            _stopwatch.Restart();
            _totalOperations = 0;
        }

        public static void IncrementOperations(long count = 1)
        {
            Interlocked.Add(ref _totalOperations, count);
        }

        public static (long Operations, long TimeMs, long OpsPerSecond) StopMeasure()
        {
            _stopwatch.Stop();
            _totalTimeMs = _stopwatch.ElapsedMilliseconds;

            var opsPerSecond = _totalTimeMs > 0
                ? _totalOperations * 1000 / _totalTimeMs
                : 0;

            return (_totalOperations, _totalTimeMs, opsPerSecond);
        }

        public static void PrintMetrics(string primitiveName, (long Operations, long TimeMs, long OpsPerSecond) metrics)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"{primitiveName}:");
            Console.WriteLine($"  Операций: {metrics.Operations:N0}");
            Console.WriteLine($"  Время: {metrics.TimeMs} мс");
            Console.WriteLine($"  Операций/сек: {metrics.OpsPerSecond:N0}");
            Console.WriteLine($"  Время на операцию: {(metrics.TimeMs * 1_000_000.0 / metrics.Operations):F2} нс");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}