using System;
using System.Collections.Generic;
using System.Text;

namespace SynchronizationPrimitives.Examples
{
    /// <summary>
    /// Mutex - для межпроцессной синхронизации
    /// Медленный (kernel-mode), но необходим для работы между процессами
    /// Межпроцессная синхронизация - работа между разными EXE
    /// </summary>
    public static class MutexExample
    {
        public static async Task Demo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nMutex");
            Console.ResetColor();

            // 1. Single-instance приложение
            Console.WriteLine("\n1. Гарантия единственного экземпляра приложения:");

            bool createdNew;
            using (var mutex = new Mutex(true, "MyUniqueAppMutex", out createdNew))
            {
                if (createdNew)
                {
                    Console.WriteLine("Это первый экземпляр приложения");

                    // Имитация работы приложения
                    await Task.Delay(2000);

                    Console.WriteLine("Завершаем работу...");
                }
                else
                {
                    Console.WriteLine("Приложение уже запущено! Завершаем...");
                    return;
                }
            }

            // 2. Защита файла между процессами
            Console.WriteLine("\n2. Синхронизация доступа к файлу между процессами:");

            var fileMutex = new Mutex(false, "Global\\MyApp_ConfigFileMutex");

            try
            {
                // Ожидаем мьютекс не более 5 секунд
                if (fileMutex.WaitOne(TimeSpan.FromSeconds(5)))
                {
                    try
                    {
                        Console.WriteLine("Захватили эксклюзивный доступ к файлу");

                        // Имитация работы с файлом
                        string filePath = "shared_config.txt";
                        string content = File.Exists(filePath)
                            ? File.ReadAllText(filePath)
                            : "";

                        Console.WriteLine($"Текущее содержимое: {content}");

                        // "Изменяем" файл
                        File.WriteAllText(filePath,
                            $"Обновлено процессом {Environment.ProcessId} в {DateTime.Now}");

                        Thread.Sleep(1000); // Имитация долгой операции
                    }
                    finally
                    {
                        fileMutex.ReleaseMutex();
                        Console.WriteLine("Освободили доступ к файлу");
                    }
                }
                else
                {
                    Console.WriteLine("Не удалось получить доступ к файлу (таймаут)");
                }
            }
            finally
            {
                fileMutex.Dispose();
            }

            // 3. Реентерабельный характер Mutex
            Console.WriteLine("\n3. Реентерабельность Mutex (повторный захват тем же потоком):");

            var reentrantMutex = new Mutex();

            try
            {
                reentrantMutex.WaitOne();
                Console.WriteLine("Первый захват мьютекса");

                // Тот же поток может захватить мьютекс снова
                reentrantMutex.WaitOne();
                Console.WriteLine("Второй захват тем же потоком (реентерабельный)");

                reentrantMutex.ReleaseMutex(); // Освобождаем второй захват
                reentrantMutex.ReleaseMutex(); // Освобождаем первый захват

                Console.WriteLine("Реентерабельность работает");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
            finally
            {
                reentrantMutex.Dispose();
            }

            // 4. Работа с несколькими мьютексами (аккуратно!)
            Console.WriteLine("\n4. Работа с несколькими мьютексами (риск deadlock):");

            var mutex1 = new Mutex(false, "Mutex1");
            var mutex2 = new Mutex(false, "Mutex2");

            try
            {
                var task1 = Task.Run(() =>
                {
                    mutex1.WaitOne();
                    Console.WriteLine("Task1: захватил Mutex1");
                    Thread.Sleep(100);

                    if (mutex2.WaitOne(TimeSpan.FromSeconds(1)))
                    {
                        try
                        {
                            Console.WriteLine("Task1: захватил Mutex2");
                        }
                        finally
                        {
                            mutex2.ReleaseMutex();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Task1: не удалось захватить Mutex2 (таймаут)");
                    }

                    mutex1.ReleaseMutex();
                });

                var task2 = Task.Run(() =>
                {
                    mutex2.WaitOne();
                    Console.WriteLine("Task2: захватил Mutex2");
                    Thread.Sleep(150);

                    if (mutex1.WaitOne(TimeSpan.FromSeconds(1)))
                    {
                        try
                        {
                            Console.WriteLine("Task2: захватил Mutex1");
                        }
                        finally
                        {
                            mutex1.ReleaseMutex();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Task2: не удалось захватить Mutex1 (таймаут)");
                    }

                    mutex2.ReleaseMutex();
                });

                await Task.WhenAll(task1, task2);
            }
            finally
            {
                mutex1.Dispose();
                mutex2.Dispose();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nПравила использования Mutex:");
            Console.WriteLine(" - Всегда использовать using или Dispose()");
            Console.WriteLine(" - Всегда использовать try-finally для ReleaseMutex()");
            Console.WriteLine(" - Использовать таймауты для избежания deadlock");
            Console.WriteLine(" - Использовать только если нужна межпроцессная синхронизация");
            Console.ResetColor();
        }
    }
}