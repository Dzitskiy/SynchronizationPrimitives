using System;
using System.Collections.Generic;
using System.Text;

namespace SynchronizationPrimitives.Examples
{
    /// <summary>
    /// Сигнальные примитивы
    /// Сигнализация и координация между потоками
    /// ManualResetEventSlim и AutoResetEvent - сигнализация между потоками
    /// </summary>
    public static class EventExample
    {
        public static async Task Demo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nEvent Primitives");
            Console.ResetColor();

            // 1. ManualResetEventSlim - "шлагбаум"
            Console.WriteLine("\n1. ManualResetEventSlim - ожидание инициализации:");

            var initializationComplete = new ManualResetEventSlim(false);
            string sharedData = null;

            var initializerTask = Task.Run(() =>
            {
                Console.WriteLine("Инициализация начата...");
                Thread.Sleep(2000); // Имитация долгой инициализации
                sharedData = "Данные загружены";
                Console.WriteLine("Инициализация завершена!");

                initializationComplete.Set(); // Открываем "шлагбаум" для всех
            });

            // Несколько потребителей ждут инициализации
            var consumerTasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                int consumerId = i;
                consumerTasks.Add(Task.Run(() =>
                {
                    Console.WriteLine($"Потребитель {consumerId} ждет инициализации...");
                    initializationComplete.Wait(); // Ждем сигнала
                    Console.WriteLine($"Потребитель {consumerId} получил: {sharedData}");
                }));
            }

            await Task.WhenAll(new[] { initializerTask }.Concat(consumerTasks));

            // 2. AutoResetEvent - "турникет" (один поток за раз)
            Console.WriteLine("\n2. AutoResetEvent - поточная обработка:");

            var itemAvailable = new AutoResetEvent(false);
            var queue = new Queue<int>();
            var stopSignal = new ManualResetEventSlim(false);

            // Producer
            var producerTask = Task.Run(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    lock (queue)
                    {
                        queue.Enqueue(i);
                        Console.WriteLine($"Producer: добавил {i}");
                    }

                    itemAvailable.Set(); // Сигнализируем, что есть элемент
                    Thread.Sleep(100);
                }

                Thread.Sleep(500);
                stopSignal.Set(); // Сигнал остановки
                itemAvailable.Set(); // Последний сигнал для пробуждения потребителей
            });

            // Consumer
            var consumerTask = Task.Run(() =>
            {
                while (true)
                {
                    // Ждем сигнала или остановки
                    int signalIndex = WaitHandle.WaitAny(new[]
                    {
                    itemAvailable ,
                    stopSignal.WaitHandle
                });

                    if (signalIndex == 1) // stopSignal
                        break;

                    lock (queue)
                    {
                        if (queue.Count > 0)
                        {
                            int item = queue.Dequeue();
                            Console.WriteLine($"Consumer: обработал {item}");
                        }
                    }
                }
                Console.WriteLine("Consumer завершил работу");
            });

            await Task.WhenAll(producerTask, consumerTask);

            // 3. ManualResetEventSlim с таймаутом и отменой
            Console.WriteLine("\n3. Ожидание с таймаутом и CancellationToken:");

            var waitEvent = new ManualResetEventSlim(false);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            var waitingTask = Task.Run(() =>
            {
                try
                {
                    Console.WriteLine("Начинаем ожидание события (таймаут 3 секунды)...");

                    // Ожидание с поддержкой отмены
                    bool signaled = waitEvent.Wait(3000, cts.Token);

                    if (signaled)
                        Console.WriteLine("Событие получено!");
                    else
                        Console.WriteLine("Таймаут или отмена");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Ожидание отменено");
                }
            });

            // Имитация работы, которая НЕ установит событие
            await Task.Delay(100);
            Console.WriteLine("Работа продолжается...");

            await waitingTask;

            // 4. Паттерн двойной проверки с Event
            Console.WriteLine("\n4. Паттерн двойной проверки (Double-Check) с Event:");

            var cacheReady = new ManualResetEventSlim(false);
            bool isCacheLoaded = false;
            object cache = null;

            var loaderTasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                int threadId = i;
                loaderTasks.Add(Task.Run(() =>
                {
                    // Первая проверка (быстрая)
                    if (isCacheLoaded)
                    {
                        Console.WriteLine($"Поток {threadId}: кэш уже загружен");
                        return;
                    }

                    // Ожидаем события загрузки
                    cacheReady.Wait();

                    // Вторая проверка после события
                    if (isCacheLoaded)
                    {
                        Console.WriteLine($"Поток {threadId}: использует загруженный кэш");
                    }
                }));
            }

            // Загрузчик кэша
            var cacheLoader = Task.Run(() =>
            {
                Thread.Sleep(500);
                Console.WriteLine("Загрузка кэша...");
                Thread.Sleep(1000);

                cache = new { Data = "Кэшированные данные" };
                isCacheLoaded = true;
                cacheReady.Set(); // Уведомляем всех ожидающих

                Console.WriteLine("Кэш загружен и готов к использованию");
            });

            await Task.WhenAll(loaderTasks.Concat(new[] { cacheLoader }));

            // 5. Сравнение производительности Slim vs Kernel
            Console.WriteLine("\n5. Сравнение ManualResetEventSlim и ManualResetEvent:");

            var slimEvent = new ManualResetEventSlim(false);
            var kernelEvent = new ManualResetEvent(false);

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Тест Slim версии
            var slimTasks = new List<Task>();
            for (int i = 0; i < 1000; i++)
            {
                slimTasks.Add(Task.Run(() =>
                {
                    slimEvent.Wait();
                }));
            }

            await Task.Delay(10);
            slimEvent.Set();
            await Task.WhenAll(slimTasks);

            sw.Stop();
            Console.WriteLine($"ManualResetEventSlim: {sw.ElapsedMilliseconds} мс");

            sw.Restart();

            // Тест Kernel версии
            var kernelTasks = new List<Task>();
            for (int i = 0; i < 1000; i++)
            {
                kernelTasks.Add(Task.Run(() =>
                {
                    kernelEvent.WaitOne();
                }));
            }

            await Task.Delay(10);
            kernelEvent.Set();
            await Task.WhenAll(kernelTasks);

            sw.Stop();
            Console.WriteLine($"ManualResetEvent: {sw.ElapsedMilliseconds} мс");

            kernelEvent.Dispose();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nПравила использования Event примитивов:");
            Console.WriteLine(" - ManualResetEventSlim - когда нужно уведомить МНОГИХ потоков");
            Console.WriteLine(" - AutoResetEvent - когда нужно уведомить ТОЛЬКО ОДИН поток");
            Console.WriteLine(" - Всегда использовать Slim-версии для внутрипроцессной синхронизации");
            Console.WriteLine(" - Использовать WaitHandle.WaitAny/WaitAll для сложных сценариев");
            Console.ResetColor();
        }
    }
}
