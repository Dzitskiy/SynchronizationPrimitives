using SynchronizationPrimitives.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace SynchronizationPrimitives.Examples
{
    /// <summary>
    /// SpinLock - для очень коротких критических секций
    /// НЕЛЬЗЯ использовать для долгих операций или IO
    /// Идеаленый вариант для микро-оптимизаций, при блокировке на наносекунды"
    /// </summary>
    public static class SpinLockExample
    {
        private struct DataPoint
        {
            public int X;
            public int Y;
            public double Value;
        }

        public static async Task Demo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("SpinLock блокировка");
            Console.ResetColor();

            // 1. Базовое использование
            Console.WriteLine("\n1. Защита небольшой структуры данных:");

            var spinLock = new SpinLock(enableThreadOwnerTracking: true);
            var sharedData = new DataPoint[1000];
            int dataIndex = 0;

            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    var random = new Random(threadId);
                    for (int j = 0; j < 100; j++)
                    {
                        bool lockTaken = false;
                        try
                        {
                            spinLock.Enter(ref lockTaken);

                            // Очень быстрая операция - идеально для SpinLock
                            if (dataIndex < sharedData.Length)
                            {
                                sharedData[dataIndex] = new DataPoint
                                {
                                    X = random.Next(100),
                                    Y = random.Next(100),
                                    Value = random.NextDouble()
                                };
                                dataIndex++;
                            }
                        }
                        finally
                        {
                            if (lockTaken)
                                spinLock.Exit();
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            Console.WriteLine($"Добавлено {dataIndex} элементов");

            // 2. Сравнение с обычным lock (Monitor)
            Console.WriteLine("\n2. Сравнение SpinLock vs Monitor для быстрых операций:");

            var simpleLock = new object();
            int counter1 = 0;
            int counter2 = 0;
            var spinLock2 = new SpinLock();

            // Тест с обычным lock
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Parallel.For(0, 1_000_000, i =>
            {
                lock (simpleLock)
                {
                    counter1++;
                }
            });
            sw.Stop();
            Console.WriteLine($"Monitor (lock): {sw.ElapsedMilliseconds} мс");

            // Тест с SpinLock
            sw.Restart();
            Parallel.For(0, 1_000_000, i =>
            {
                bool lockTaken = false;
                try
                {
                    spinLock2.Enter(ref lockTaken);
                    counter2++;
                }
                finally
                {
                    if (lockTaken)
                        spinLock2.Exit();
                }
            });
            sw.Stop();
            Console.WriteLine($"SpinLock: {sw.ElapsedMilliseconds} мс");

            // 3. Опасный пример: что происходит при долгой операции
            Console.WriteLine("\n3. ВНИМАНИЕ: SpinLock с долгой операцией (плохой пример):");

            var badSpinLock = new SpinLock();
            int badCounter = 0;

            // Эмуляция долгой операции под SpinLock - ЭТО ОЧЕНЬ ПЛОХО!
            try
            {
                bool lockTaken = false;
                badSpinLock.Enter(ref lockTaken);

                // Симуляция долгой операции
                Thread.Sleep(100); // Никогда не делайте этого под SpinLock!
                badCounter++;

                if (lockTaken)
                    badSpinLock.Exit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка (ожидаемо): {ex.Message}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nПравила использования SpinLock:");
            Console.WriteLine(" - Только для ОЧЕНЬ быстрых операций (< 1000 тактов CPU)");
            Console.WriteLine(" - Никогда не вызывать блокирующие операции");
            Console.WriteLine(" - Не использовать в асинхронном коде");
            Console.WriteLine(" - Избегать на гиперарных системах (много ядер)");
            Console.ResetColor();
        }

        /// <summary>
        /// Метод проверки производительности
        /// </summary>
        /// <param name="iterations">Количество итераций</param>
        /// <returns></returns>
        public static async Task PerformanceTest(int iterations)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\nТест производительности SpinLock ({iterations:N0} итераций):");
            Console.ResetColor();

            var spinLock = new SpinLock();
            int counter = 0;

            MetricsCollector.StartMeasure();

            var tasks = new List<Task>();
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < iterations / Environment.ProcessorCount; j++)
                    {
                        bool lockTaken = false;
                        try
                        {
                            spinLock.Enter(ref lockTaken);
                            counter++;
                            MetricsCollector.IncrementOperations();
                        }
                        finally
                        {
                            if (lockTaken)
                                spinLock.Exit();
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            var metrics = MetricsCollector.StopMeasure();
            MetricsCollector.PrintMetrics("SpinLock", metrics);
        }
    }
}