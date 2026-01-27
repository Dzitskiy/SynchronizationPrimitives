using SynchronizationPrimitives.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace SynchronizationPrimitives.Examples
{
    /// <summary>
    /// ReaderWriterLockSlim - оптимизация для сценариев "часто читают, редко пишут"
    /// Оптимизация для частого чтения и редкой записи"
    /// </summary>
    public static class ReaderWriterExample
    {
        private static readonly ReaderWriterLockSlim _cacheLock = new(LockRecursionPolicy.NoRecursion);
        private static readonly Dictionary<string, string> _configCache = new();
        private static DateTime _lastRefreshTime;

        public static async Task Demo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nReaderWriterLockSlim");
            Console.ResetColor();

            // Инициализация кэша
            _configCache["Theme"] = "Dark";
            _configCache["Language"] = "Russian";
            _lastRefreshTime = DateTime.UtcNow;

            // 1. Многопоточное чтение конфигурации
            Console.WriteLine("\n1. Многопоточный доступ к кэшу конфигурации:");

            var readerTasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                int readerId = i;
                readerTasks.Add(Task.Run(() =>
                {
                    // Множество читателей могут работать одновременно
                    _cacheLock.EnterReadLock();
                    try
                    {
                        Console.WriteLine($"Читатель {readerId}: Theme={_configCache["Theme"]}, " +
                                         $"Language={_configCache["Language"]}");
                        Thread.Sleep(10); // Имитация работы
                    }
                    finally
                    {
                        _cacheLock.ExitReadLock();
                    }
                }));
            }

            // Параллельно запускаем писателя
            var writerTask = Task.Run(async () =>
            {
                await Task.Delay(50); // Ждем, чтобы читатели начали работу

                // Только один писатель может работать в данный момент
                _cacheLock.EnterWriteLock();
                try
                {
                    Console.WriteLine("\n--- Писатель обновляет кэш ---");
                    _configCache["Theme"] = "Light";
                    _configCache["Language"] = "English";
                    _lastRefreshTime = DateTime.UtcNow;
                    Thread.Sleep(100); // Имитация долгой операции записи
                    Console.WriteLine("--- Кэш обновлен ---\n");
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }
            });

            await Task.WhenAll(readerTasks.Concat(new[] { writerTask }));

            // 2. Upgradeable read lock (чтение с возможностью перехода в запись)
            Console.WriteLine("\n2. Upgradeable read lock (Lazy Load паттерн):");

            string GetOrAddConfig(string key, Func<string> valueFactory)
            {
                // Сначала пытаемся прочитать
                _cacheLock.EnterUpgradeableReadLock();
                try
                {
                    if (_configCache.TryGetValue(key, out var value))
                    {
                        Console.WriteLine($"Кэш попадание для ключа '{key}'");
                        return value;
                    }

                    // Кэш промах - нужно добавить значение
                    Console.WriteLine($"Кэш промах для ключа '{key}', добавляем...");

                    _cacheLock.EnterWriteLock();
                    try
                    {
                        // Двойная проверка (double-check)
                        if (!_configCache.TryGetValue(key, out value))
                        {
                            value = valueFactory();
                            _configCache[key] = value;
                            Console.WriteLine($"Добавлено в кэш: {key}={value}");
                        }
                        return value;
                    }
                    finally
                    {
                        _cacheLock.ExitWriteLock();
                    }
                }
                finally
                {
                    _cacheLock.ExitUpgradeableReadLock();
                }
            }

            // Тестируем Lazy Load
            var lazyTasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                int taskId = i;
                lazyTasks.Add(Task.Run(() =>
                {
                    string value = GetOrAddConfig($"Key{taskId % 3}", () =>
                    {
                        Thread.Sleep(200); // Имитация долгой операции получения значения
                        return $"Value-{Guid.NewGuid().ToString()[..8]}";
                    });
                    Console.WriteLine($"Получено: Key{taskId % 3}={value}");
                }));
            }

            await Task.WhenAll(lazyTasks);

            // 3. Таймауты и избегание deadlock
            Console.WriteLine("\n3. Использование таймаутов для избежания deadlock:");

            var rwLock = new ReaderWriterLockSlim();

            var task1 = Task.Run(() =>
            {
                if (rwLock.TryEnterReadLock(TimeSpan.FromSeconds(1)))
                {
                    try
                    {
                        Console.WriteLine("Task1: захватил read lock");
                        
                        Thread.Sleep(500);

                        ////DEADLOCK / LockRecursionException
                        rwLock.ExitReadLock();
                        // Попытка получить write lock 
                        if (rwLock.TryEnterWriteLock(TimeSpan.FromMilliseconds(100)))
                        {
                            try
                            {
                                Console.WriteLine("Task1: захватил write lock");
                            }
                            finally
                            {
                                rwLock.ExitWriteLock();
                            }
                        }
                        else
                        {
                            Console.WriteLine("Task1: не удалось получить write lock (таймаут)");
                        }
                    }
                    finally
                    {
                        //rwLock.ExitReadLock();
                    }
                }
            });

            var task2 = Task.Run(() =>
            {
                Thread.Sleep(100); // Даем Task1 начать
                if (rwLock.TryEnterReadLock(TimeSpan.FromSeconds(1)))
                {
                    try
                    {
                        Console.WriteLine("Task2: захватил read lock");
                        Thread.Sleep(1000);
                    }
                    finally
                    {
                        rwLock.ExitReadLock();
                    }
                }
            });

            await Task.WhenAll(task1, task2);

            // 4. Опасности рекурсии
            Console.WriteLine("\n4. ВНИМАНИЕ: рекурсивные блокировки (опасно!):");

            var recursiveLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

            try
            {
                recursiveLock.EnterReadLock();
                Console.WriteLine("Первое чтение");

                // Рекурсивный захват - возможен deadlock в сложных сценариях
                recursiveLock.EnterReadLock();
                Console.WriteLine("Рекурсивное чтение");

                // Попытка получить write lock - DEADLOCK!
                // recursiveLock.EnterWriteLock(); // Раскомментировать для deadlock

                recursiveLock.ExitReadLock();
                recursiveLock.ExitReadLock();

                Console.WriteLine("Рекурсия работает, но лучше избегать");
            }
            catch (LockRecursionException ex)
            {
                Console.WriteLine($"Ошибка рекурсии: {ex.Message}");
            }

            Console.WriteLine("\nПравила использования ReaderWriterLockSlim:");
            Console.WriteLine(" - Всегда использовать try-finally для ExitReadLock/ExitWriteLock");
            Console.WriteLine(" - Использовать LockRecursionPolicy.NoRecursion (по умолчанию)");
            Console.WriteLine(" - Избегать EnterUpgradeableReadLock если возможно");
            Console.WriteLine(" - Использовать только когда чтения >> записей (80/20 правило)");
        }

        /// <summary>
        /// Метод проверки производительности
        /// </summary>
        /// <param name="iterations">Количество итераций</param>
        /// <returns></returns>
        public static async Task PerformanceTest(int iterations)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\nТест производительности ReaderWriterLockSlim ({iterations:N0} итераций):");
            Console.ResetColor();

            var rwLock = new ReaderWriterLockSlim();
            int readCounter = 0;
            int writeCounter = 0;

            MetricsCollector.StartMeasure();

            // 90% чтений, 10% записей (типичный сценарий для RW lock)
            var tasks = new List<Task>();
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var random = new Random();
                    for (int j = 0; j < iterations / Environment.ProcessorCount; j++)
                    {
                        if (random.Next(100) < 90) // 90% чтений
                        {
                            rwLock.EnterReadLock();
                            try
                            {
                                // Быстрая операция чтения
                                int _ = readCounter;
                                MetricsCollector.IncrementOperations();
                            }
                            finally
                            {
                                rwLock.ExitReadLock();
                            }
                        }
                        else // 10% записей
                        {
                            rwLock.EnterWriteLock();
                            try
                            {
                                writeCounter++;
                                MetricsCollector.IncrementOperations();
                            }
                            finally
                            {
                                rwLock.ExitWriteLock();
                            }
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            var metrics = MetricsCollector.StopMeasure();

            Console.WriteLine($"Чтений: {readCounter}, Записей: {writeCounter}");
            MetricsCollector.PrintMetrics("ReaderWriterLockSlim (90/10)", metrics);
        }
    }
}
