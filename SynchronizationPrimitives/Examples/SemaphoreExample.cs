using System;
using System.Collections.Generic;
using System.Text;

namespace SynchronizationPrimitives.Examples
{
    /// <summary>
    /// SemaphoreSlim - ограничение количества одновременных операций
    /// Ограничение параллелизма и пулы ресурсов
    /// Идеален для пулов ресурсов и rate limiting
    /// </summary>
    public static class SemaphoreExample
    {
        public static async Task Demo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nSemaphoreSlim");
            Console.ResetColor();

            // 1. Базовое ограничение параллелизма
            Console.WriteLine("\n1. Ограничение параллельных HTTP запросов:");

            var semaphore = new SemaphoreSlim(3, 3); // Максимум 3 параллельных запроса
            var httpTasks = new List<Task>();
            int requestCounter = 0;

            for (int i = 0; i < 10; i++)
            {
                int requestId = i;
                httpTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        Interlocked.Increment(ref requestCounter);
                        Console.WriteLine($"Запрос {requestId} начат (активных: {3 - semaphore.CurrentCount})");

                        // Имитация HTTP запроса
                        await Task.Delay(1000);

                        Console.WriteLine($"Запрос {requestId} завершен");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(httpTasks);
            Console.WriteLine($"Всего выполнено запросов: {requestCounter}");

            // 2. Пул подключений к базе данных
            Console.WriteLine("\n2. Пул подключений к БД (имитация):");

            var dbConnections = new List<string>
        {
            "Connection-1", "Connection-2", "Connection-3"
        };
            var dbSemaphore = new SemaphoreSlim(dbConnections.Count, dbConnections.Count);
            var dbTasks = new List<Task<string>>();

            for (int i = 0; i < 10; i++)
            {
                dbTasks.Add(Task.Run(async () =>
                {
                    await dbSemaphore.WaitAsync();
                    string connection = null;

                    try
                    {
                        // Берём свободное подключение
                        lock (dbConnections)
                        {
                            if (dbConnections.Count > 0)
                            {
                                connection = dbConnections[0];
                                dbConnections.RemoveAt(0);
                            }
                        }

                        if (connection != null)
                        {
                            Console.WriteLine($"Используем {connection} для запроса");
                            await Task.Delay(500); // Имитация запроса
                            return $"Результат с {connection}";
                        }

                        return "Ошибка: нет доступных подключений";
                    }
                    finally
                    {
                        // Возвращаем подключение в пул
                        if (connection != null)
                        {
                            lock (dbConnections)
                            {
                                dbConnections.Add(connection);
                            }
                        }
                        dbSemaphore.Release();
                    }
                }));
            }

            var results = await Task.WhenAll(dbTasks);
            Console.WriteLine($"Выполнено {results.Length} запросов к БД");

            // 3. Async-версия с WaitAsync (современный подход)
            Console.WriteLine("\n3. Асинхронное ограничение с WaitAsync:");

            var asyncSemaphore = new SemaphoreSlim(2, 2);
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var asyncTasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                int taskId = i;
                asyncTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Асинхронное ожидание с возможностью отмены
                        await asyncSemaphore.WaitAsync(cancellationToken);

                        try
                        {
                            Console.WriteLine($"Задача {taskId} начала выполнение");
                            await Task.Delay(2000, cancellationToken);
                            Console.WriteLine($"Задача {taskId} завершена");
                        }
                        finally
                        {
                            asyncSemaphore.Release();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"Задача {taskId} отменена");
                    }
                }));
            }

            // Отменяем через 3 секунды
            await Task.Delay(3000);
            cancellationTokenSource.Cancel();

            try
            {
                await Task.WhenAll(asyncTasks);
            }
            catch (AggregateException)
            {
                // Ожидаемые исключения отмены
            }

            // 4. Межпроцессный Semaphore (для Windows)
            Console.WriteLine("\n4. Межпроцессный Semaphore (только для Windows):");

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    using (var crossProcessSemaphore = new Semaphore(2, 2, "Global\\MyCrossProcessSemaphore"))
                    {
                        if (crossProcessSemaphore.WaitOne(TimeSpan.FromSeconds(1)))
                        {
                            try
                            {
                                Console.WriteLine("Захватили межпроцессный семафор");
                                Console.WriteLine("Имитация работы с общим ресурсом...");
                                await Task.Delay(500);
                            }
                            finally
                            {
                                crossProcessSemaphore.Release();
                                Console.WriteLine("Освободили межпроцессный семафор");
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("Нет прав для создания/открытия межпроцессного семафора");
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nПравила использования SemaphoreSlim:");
            Console.WriteLine(" - Использовать WaitAsync() для асинхронного кода");
            Console.WriteLine(" - Всегда использовать try-finally для Release()");
            Console.WriteLine(" - Устанавливать разумные лимиты (не 1 и не 1000)");
            Console.WriteLine(" - Использовать для rate limiting и пулов ресурсов");
            Console.ResetColor();
        }
    }
}