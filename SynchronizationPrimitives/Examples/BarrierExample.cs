using System;
using System.Collections.Generic;
using System.Text;

namespace SynchronizationPrimitives.Examples
{
    /// <summary>
    /// Координация по фазам
    /// </summary>
    /// <summary>
    /// Barrier - координация потоков по фазам (синхронные этапы)
    /// Сложные параллельные алгоритмы, где нужны результаты всех потоков на каждом этапе
    /// </summary>
    public static class BarrierExample
    {
        public static async Task Demo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nBarrier");
            Console.ResetColor();

            // 1. Простой пример: параллельная обработка с барьерами
            Console.WriteLine("\n1. Параллельная обработка данных по фазам:");

            var data = new int[100];
            for (int i = 0; i < data.Length; i++)
                data[i] = Random.Shared.Next(1, 100);

            var barrier = new Barrier(3, (b) =>
            {
                Console.WriteLine($"Фаза {b.CurrentPhaseNumber} завершена всеми потоками");
            });

            var results = new List<int>[3];
            for (int i = 0; i < results.Length; i++)
                results[i] = new List<int>();

            var tasks = new Task[3];
            for (int i = 0; i < 3; i++)
            {
                int threadId = i;
                tasks[threadId] = Task.Run(() =>
                {
                    // Фаза 1: Фильтрация данных
                    int start = threadId * 33;
                    int end = Math.Min(start + 33, data.Length);

                    for (int j = start; j < end; j++)
                    {
                        if (data[j] > 50)
                            results[threadId].Add(data[j]);
                    }

                    Console.WriteLine($"Поток {threadId}: Фаза 1 завершена, найдено {results[threadId].Count} элементов");
                    barrier.SignalAndWait();

                    // Фаза 2: Сортировка
                    results[threadId].Sort();
                    Console.WriteLine($"Поток {threadId}: Фаза 2 завершена, отсортировано {results[threadId].Count} элементов");
                    barrier.SignalAndWait();

                    // Фаза 3: Объединение результатов
                    if (threadId == 0)
                    {
                        var finalResult = new List<int>();
                        foreach (var list in results)
                            finalResult.AddRange(list);

                        finalResult.Sort();
                        Console.WriteLine($"Финальный результат: {finalResult.Count} элементов");
                    }

                    barrier.SignalAndWait();
                });
            }

            await Task.WhenAll(tasks);

            // 2. Matrix multiplication с Barrier
            Console.WriteLine("\n2. Параллельное перемножение матриц с барьерами:");

            int size = 100;
            var matrixA = CreateMatrix(size);
            var matrixB = CreateMatrix(size);
            var resultMatrix = new double[size, size];

            var matrixBarrier = new Barrier(
                participantCount: 4,
                (b) =>
                {
                    Console.WriteLine($"Фаза перемножения {b.CurrentPhaseNumber} завершена");
                });

            var matrixTasks = new Task[4];
            for (int t = 0; t < 4; t++)
            {
                int threadNum = t;
                matrixTasks[t] = Task.Run(() =>
                {
                    int startRow = threadNum * size / 4;
                    int endRow = (threadNum + 1) * size / 4;

                    for (int phase = 0; phase < size; phase++)
                    {
                        for (int i = startRow; i < endRow; i++)
                        {
                            double sum = 0;
                            for (int k = 0; k < size; k++)
                            {
                                sum += matrixA[i, k] * matrixB[k, phase];
                            }
                            resultMatrix[i, phase] = sum;
                        }

                        // Синхронизируем после вычисления каждого столбца
                        matrixBarrier.SignalAndWait();
                    }
                });
            }

            await Task.WhenAll(matrixTasks);
            Console.WriteLine($"Перемножение матриц {size}x{size} завершено");

            // 3. Обработка с пост-фазным действием
            Console.WriteLine("\n3. Сложное пост-фазное действие:");

            var complexBarrier = new Barrier(3, (b) =>
            {
                Console.WriteLine($"--- Все потоки достигли барьера фазе {b.CurrentPhaseNumber} ---");
                Console.WriteLine($"Участников: {b.ParticipantCount}, Ожидается: {b.ParticipantsRemaining}");
                Thread.Sleep(100); // Имитация пост-обработки
            });

            var complexTasks = new Task[3];
            int[] threadResults = new int[3];

            for (int i = 0; i < 3; i++)
            {
                int threadId = i;
                complexTasks[i] = Task.Run(() =>
                {
                    for (int phase = 0; phase < 3; phase++)
                    {
                        // Каждая фаза увеличивает результат на случайное значение
                        threadResults[threadId] += Random.Shared.Next(1, 10);
                        Console.WriteLine($"Поток {threadId}: фаза {phase}, результат: {threadResults[threadId]}");

                        // Синхронизация
                        complexBarrier.SignalAndWait();

                        // После барьера можем использовать результаты других потоков
                        if (threadId == 0)
                        {
                            int total = threadResults.Sum();
                            Console.WriteLine($"Общий результат после фазы {phase}: {total}");
                        }

                        complexBarrier.SignalAndWait();
                    }
                });
            }

            await Task.WhenAll(complexTasks);

            // 4. Добавление и удаление участников
            Console.WriteLine("\n4. Динамическое изменение количества участников:");

            var dynamicBarrier = new Barrier(1, (b) =>
            {
                Console.WriteLine($"Фаза завершена. Участников: {b.ParticipantCount}");
            });

            async Task Worker(int id, int phases)
            {
                // Регистрируем участника
                dynamicBarrier.AddParticipant();
                Console.WriteLine($"Рабочий {id} присоединился");

                try
                {
                    for (int phase = 0; phase < phases; phase++)
                    {
                        await Task.Delay(Random.Shared.Next(50, 200));
                        Console.WriteLine($"Рабочий {id} завершил фазу {phase}");
                        dynamicBarrier.SignalAndWait(100);
                    }
                }
                finally
                {
                    // Удаляем участника
                    dynamicBarrier.RemoveParticipant();
                    Console.WriteLine($"Рабочий {id} завершил работу");
                }
            }

            // Запускаем рабочих с разным количеством фаз
            var workerTasks = new[]
            {
            Worker(1, 3),
            Worker(2, 2),
            Worker(3, 4)
        };

            await Task.WhenAll(workerTasks);

            // 5. Обработка исключений и отмена
            Console.WriteLine("\n5. Обработка исключений в Barrier:");

            var exceptionBarrier = new Barrier(3);
            var cts = new CancellationTokenSource();

            var exceptionTasks = new Task[3];
            for (int i = 0; i < 3; i++)
            {
                int threadId = i;
                exceptionTasks[i] = Task.Run(() =>
                {
                    try
                    {
                        for (int phase = 0; phase < 3; phase++)
                        {
                            if (threadId == 1 && phase == 1)
                                throw new InvalidOperationException($"Исключение в потоке {threadId}");

                            if (cts.Token.IsCancellationRequested)
                                break;

                            Console.WriteLine($"Поток {threadId}: фаза {phase}");
                            exceptionBarrier.SignalAndWait(cts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"Поток {threadId} отменен");
                    }
                    catch (BarrierPostPhaseException bppe)
                    {
                        Console.WriteLine($"Ошибка в пост-фазном действии: {bppe.InnerException?.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Поток {threadId}: {ex.Message}");
                        cts.Cancel();
                    }
                });
            }

            try
            {
                await Task.WhenAll(exceptionTasks);
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.Flatten().InnerExceptions)
                    Console.WriteLine($"Собрано исключение: {e.Message}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nПравила использования Barrier:");
            Console.WriteLine(" - Идеален для алгоритмов, требующих синхронизации по фазам");
            Console.WriteLine(" - PhaseAction выполняется ОДНИМ потоком после каждой фазы");
            Console.WriteLine(" - Обрабатывайте BarrierPostPhaseException в PhaseAction");
            Console.ResetColor();
        }

        private static double[,] CreateMatrix(int size)
        {
            var matrix = new double[size, size];
            var random = new Random();

            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                    matrix[i, j] = random.NextDouble();

            return matrix;
        }
    }
}
