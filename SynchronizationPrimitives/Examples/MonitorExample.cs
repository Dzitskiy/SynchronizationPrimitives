using SynchronizationPrimitives.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace SynchronizationPrimitives.Examples
{
    /// <summary>
    /// Monitor (и lock) - стандарт для эксклюзивного доступа
    /// Гибридная реализация: spin + kernel object
    /// Стандартный вариант для защиты общего ресурса
    /// </summary>
    public static class MonitorExample
    {
        private static readonly object _lockObject = new();
        private static readonly List<string> _sharedLog = new();

        public static async Task Demo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nMonitor и конструкция lock");
            Console.ResetColor();

            // 1. Базовое использование lock
            Console.WriteLine("\n1. Защита общего списка:");

            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        lock (_lockObject)
                        {
                            _sharedLog.Add($"Поток {threadId}: запись {j}");
                        }
                        Thread.Sleep(1); // Имитация работы
                    }
                }));
            }

            await Task.WhenAll(tasks);
            Console.WriteLine($"Всего записей в логе: {_sharedLog.Count}");

            // 2. Monitor с Wait/Pulse для producer-consumer
            Console.WriteLine("\n2. Producer-Consumer паттерн с Monitor.Wait/Pulse:");

            var queue = new Queue<int>();
            var queueLock = new object();
            bool productionComplete = false;

            var producer = Task.Run(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    lock (queueLock)
                    {
                        queue.Enqueue(i);
                        Console.WriteLine($"Producer: добавил {i}");
                        Monitor.Pulse(queueLock); // Уведомляем потребителя
                    }
                    Thread.Sleep(100);
                }

                lock (queueLock)
                {
                    productionComplete = true;
                    Monitor.PulseAll(queueLock); // Уведомляем всех
                }
            });

            var consumer = Task.Run(() =>
            {
                while (true)
                {
                    lock (queueLock)
                    {
                        while (queue.Count == 0 && !productionComplete)
                        {
                            Monitor.Wait(queueLock); // Ждем уведомления
                        }

                        if (queue.Count == 0 && productionComplete)
                            break;

                        if (queue.Count > 0)
                        {
                            int item = queue.Dequeue();
                            Console.WriteLine($"Consumer: обработал {item}");
                        }
                    }
                }
            });

            await Task.WhenAll(producer, consumer);
            Console.WriteLine("Producer-Consumer завершен");

            // 3. Monitor.TryEnter с таймаутом (защита от deadlock)
            Console.WriteLine("\n3. TryEnter с таймаутом для избежания deadlock:");

            var resource1 = new object();
            var resource2 = new object();

            var task1 = Task.Run(() =>
            {
                lock (resource1)
                {
                    Console.WriteLine("Task1 захватил resource1");
                    Thread.Sleep(50);

                    if (Monitor.TryEnter(resource2, TimeSpan.FromMilliseconds(100)))
                    {
                        try
                        {
                            Console.WriteLine("Task1 захватил resource2");
                        }
                        finally
                        {
                            Monitor.Exit(resource2);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Task1 не смог захватить resource2 (таймаут)");
                    }
                }
            });

            var task2 = Task.Run(() =>
            {
                lock (resource2)
                {
                    Console.WriteLine("Task2 захватил resource2");
                    Thread.Sleep(100);

                    lock (resource1) // Потенциальный deadlock
                    {
                        Console.WriteLine("Task2 захватил resource1");
                    }
                }
            });

            await Task.WhenAll(task1, task2);
            Console.WriteLine("Deadlock избегнут благодаря таймауту");

            // 4. Вложенные блокировки - опасный паттерн
            Console.WriteLine("\n4. ВНИМАНИЕ: вложенные блокировки (риск deadlock):");

            var lockA = new object();
            var lockB = new object();

            try
            {
                Parallel.Invoke(
                    () =>
                    {
                        lock (lockA)
                        {
                            Console.WriteLine("Поток 1: захватил lockA");
                            Thread.Sleep(100);
                            lock (lockB) // Ожидаем lockB
                            {
                                Console.WriteLine("Поток 1: захватил lockB");
                            }
                        }
                    }
                    //,
                    //() =>
                    //{
                    //    lock (lockB) // Захватываем в обратном порядке - DEADLOCK!
                    //    {
                    //        Console.WriteLine("Поток 2: захватил lockB");
                    //        Thread.Sleep(100);
                    //        lock (lockA) // Ожидаем lockA
                    //        {
                    //            Console.WriteLine("Поток 2: захватил lockA");
                    //        }
                    //    }
                    //}
                );
            }
            catch (AggregateException ex)
            {
                Console.WriteLine($"Произошел deadlock: {ex.InnerException?.Message}");
            }
        }

        /// <summary>
        /// Метод проверки производительности
        /// </summary>
        /// <param name="iterations">Количество итераций</param>
        /// <returns></returns>
        public static async Task PerformanceTest(int iterations)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\nТест производительности Monitor ({iterations:N0} итераций):");
            Console.ResetColor();

            var lockObj = new object();
            int counter = 0;

            MetricsCollector.StartMeasure();

            var tasks = new List<Task>();
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < iterations / Environment.ProcessorCount; j++)
                    {
                        lock (lockObj)
                        {
                            counter++;
                            MetricsCollector.IncrementOperations();
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            var metrics = MetricsCollector.StopMeasure();
            MetricsCollector.PrintMetrics("Monitor (lock)", metrics);
        }
    }
}