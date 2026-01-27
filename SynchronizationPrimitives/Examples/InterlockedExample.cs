using SynchronizationPrimitives.Shared;
using System.Runtime.CompilerServices;

namespace SynchronizationPrimitives.Examples
{
    /// <summary>
    /// Interlocked - самый быстрый способ для атомарных операций
    /// Идеален для счетчиков и простых флагов
    /// </summary>
    public static class InterlockedExample
    {
        private static int _counter = 0;
        private static long _sharedValue = 0;

        public static async Task Demo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Атомарные операции без блокировок");
            Console.ResetColor();

            // 1. Базовые операции
            Console.WriteLine("\n1. Базовые атомарные операции:");

            Interlocked.Increment(ref _counter);
            Console.WriteLine($"Increment: {_counter}");

            Interlocked.Decrement(ref _counter);
            Console.WriteLine($"Decrement: {_counter}");

            Interlocked.Add(ref _counter, 5);
            Console.WriteLine($"Add(5): {_counter}");

            // 2. CompareExchange - основа lock-free алгоритмов
            Console.WriteLine("\n2. CompareExchange (CAS операция):");

            int original = 10;
            int comparand = 10;
            int newValue = 20;

            int result = Interlocked.CompareExchange(ref original, newValue, comparand);
            Console.WriteLine($"Original: {result}, New value: {original}");

            // 3. Реальный пример: потокобезопасный счетчик с максимальным значением
            Console.WriteLine("\n3. Потокобезопасный счетчик с лимитом:");

            var limitedCounter = new LimitedCounter(100);
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 15; j++)
                    {
                        bool success = limitedCounter.TryIncrement();
                        if (!success)
                            Console.WriteLine($"Поток {Task.CurrentId} не смог увеличить счетчик (достигнут лимит)");
                    }
                }));
            }

            await Task.WhenAll(tasks);
            Console.WriteLine($"Финальное значение: {limitedCounter.GetValue()}");

            // 4. Lock-free обновление shared state
            Console.WriteLine("\n4. Lock-free обновление состояния:");

            var config = new Config { Value = 1, Version = 0 };
            var tasks2 = new List<Task>();

            for (int i = 0; i < 5; i++)
            {
                tasks2.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        UpdateConfig(ref config, j);
                    }
                }));
            }

            await Task.WhenAll(tasks2);
            Console.WriteLine($"Финальная конфигурация: Value={config.Value}, Version={config.Version}");
        }

        // Lock-free счетчик с ограничением
        private class LimitedCounter
        {
            private int _value = 0;
            private readonly int _maxValue;

            public LimitedCounter(int maxValue) => _maxValue = maxValue;

            public bool TryIncrement()
            {
                int current;
                int newValue;

                do
                {
                    current = _value;
                    if (current >= _maxValue)
                        return false;

                    newValue = current + 1;
                }
                while (Interlocked.CompareExchange(ref _value, newValue, current) != current);

                return true;
            }

            public int GetValue() => _value;
        }

        // Lock-free обновление конфигурации
        private struct Config
        {
            public int Value;
            public int Version;
        }

        private static void UpdateConfig(ref Config config, int newValue)
        {
            Config original, updated;

            do
            {
                original = config;
                updated = new Config
                {
                    Value = newValue,
                    Version = original.Version + 1
                };
            }
            while (Interlocked.CompareExchange(
                ref Unsafe.As<Config, int>(ref config),
                Unsafe.As<Config, int>(ref updated),
                Unsafe.As<Config, int>(ref original)) != Unsafe.As<Config, int>(ref original));
        }

        /// <summary>
        /// Метод проверки производительности
        /// </summary>
        /// <param name="iterations">Количество итераций</param>
        /// <returns></returns>
        public static async Task PerformanceTest(int iterations)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\nТест производительности Interlocked ({iterations:N0} итераций):");
            Console.ResetColor();

            MetricsCollector.StartMeasure();

            int localCounter = 0;
            var tasks = new List<Task>();

            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < iterations / Environment.ProcessorCount; j++)
                    {
                        Interlocked.Increment(ref localCounter);
                        MetricsCollector.IncrementOperations();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            var metrics = MetricsCollector.StopMeasure();
            MetricsCollector.PrintMetrics("Interlocked.Increment", metrics);
        }
    }
}