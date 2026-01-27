using System;
using System.Collections.Generic;
using System.Text;

namespace SynchronizationPrimitives.Examples
{
    /// <summary>
    /// CountdownEvent - ожидание завершения нескольких операций
    /// Удобная обёртка над ManualResetEvent для сценариев "ожидаем N сигналов"
    /// </summary>
    public static class CountdownExample
    {
        public static async Task Demo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nCountdownEvent");
            Console.ResetColor();

            // 1. Базовый пример: ожидание завершения нескольких задач
            Console.WriteLine("\n1. Ожидание завершения параллельной обработки:");

            var countdown = new CountdownEvent(10); // Ожидаем 10 сигналов
            var results = new List<string>();
            var resultsLock = new object();

            for (int i = 0; i < 10; i++)
            {
                int taskId = i;
                _ = Task.Run(() =>
                {
                    try
                    {
                        // Имитация работы
                        Thread.Sleep(Random.Shared.Next(100, 1000));
                        var result = $"Результат задачи {taskId}";

                        lock (resultsLock)
                        {
                            results.Add(result);
                        }

                        Console.WriteLine($"Задача {taskId} завершена");
                    }
                    finally
                    {
                        // Сигнализируем о завершении
                        countdown.Signal();
                    }
                });
            }

            Console.WriteLine("Ожидаем завершения всех задач...");
            countdown.Wait(); // Блокируемся, пока все 10 задач не завершатся

            Console.WriteLine($"Все задачи завершены! Получено результатов: {results.Count}");

            // 2. Динамическое добавление работы
            Console.WriteLine("\n2. Динамическое добавление задач в CountdownEvent:");

            var dynamicCountdown = new CountdownEvent(1); // Начинаем с 1
            int completedTasks = 0;

            // Главная задача
            _ = Task.Run(() =>
            {
                // Имитация обнаружения новых задач
                for (int i = 0; i < 3; i++)
                {
                    int batchId = i;
                    Console.WriteLine($"Обнаружена новая пачка задач {batchId}");

                    // Добавляем 3 задачи в текущую пачку
                    for (int j = 0; j < 3; j++)
                    {
                        dynamicCountdown.AddCount(); // Увеличиваем счетчик

                        Task.Run(() =>
                        {
                            Thread.Sleep(100);
                            Interlocked.Increment(ref completedTasks);
                            dynamicCountdown.Signal();
                        });
                    }

                    Thread.Sleep(500);
                }

                // Завершаем начальную задачу
                dynamicCountdown.Signal();
            });

            // Ожидаем завершения всех задач (включая динамически добавленные)
            dynamicCountdown.Wait();
            Console.WriteLine($"Все динамические задачи завершены: {completedTasks}");

            // 3. Ожидание с таймаутом
            Console.WriteLine("\n3. Ожидание с таймаутом (защита от зависания):");

            var timeoutCountdown = new CountdownEvent(5);

            // Запускаем только 3 задачи вместо 5
            for (int i = 0; i < 3; i++)
            {
                _ = Task.Run(() =>
                {
                    Thread.Sleep(200);
                    timeoutCountdown.Signal();
                });
            }

            // Ждем с таймаутом 1 секунда
            bool allCompleted = timeoutCountdown.Wait(TimeSpan.FromSeconds(1));

            if (allCompleted)
            {
                Console.WriteLine("Все задачи завершены вовремя");
            }
            else
            {
                Console.WriteLine($"Таймаут! Ожидалось: 5, Завершено: {5 - timeoutCountdown.CurrentCount}");
            }

            // 4. Паттерн "разделяй и властвуй" с CountdownEvent
            Console.WriteLine("\n4. Рекурсивная параллельная обработка (Divide & Conquer):");

            int ProcessTree(TreeNode node, CountdownEvent cdEvent)
            {
                if (node == null)
                    return 0;

                if (node.Left == null && node.Right == null)
                {
                    // Листовой узел - обрабатываем
                    Thread.Sleep(10);
                    return node.Value;
                }

                // Внутренний узел - обрабатываем детей параллельно
                int leftResult = 0, rightResult = 0;

                if (node.Left != null)
                {
                    cdEvent.AddCount();
                    Task.Run(() =>
                    {
                        try
                        {
                            leftResult = ProcessTree(node.Left, cdEvent);
                        }
                        finally
                        {
                            cdEvent.Signal();
                        }
                    });
                }

                if (node.Right != null)
                {
                    cdEvent.AddCount();
                    Task.Run(() =>
                    {
                        try
                        {
                            rightResult = ProcessTree(node.Right, cdEvent);
                        }
                        finally
                        {
                            cdEvent.Signal();
                        }
                    });
                }

                cdEvent.Wait(); // Ждем завершения обработки детей
                return leftResult + rightResult + node.Value;
            }

            // Создаем тестовое дерево
            var root = new TreeNode(1)
            {
                Left = new TreeNode(2)
                {
                    Left = new TreeNode(4),
                    Right = new TreeNode(5)
                },
                Right = new TreeNode(3)
                {
                    Left = new TreeNode(6),
                    Right = new TreeNode(7)
                }
            };

            var treeCountdown = new CountdownEvent(1);
            int treeSum = 0;

            _ = Task.Run(() =>
            {
                try
                {
                    treeSum = ProcessTree(root, treeCountdown);
                }
                finally
                {
                    treeCountdown.Signal();
                }
            });

            /////////treeCountdown.Wait();
            ///
            Console.WriteLine($"Сумма значений дерева: {treeSum}");

            // 5. Reset и повторное использование
            Console.WriteLine("\n5. Reset и повторное использование CountdownEvent:");

            var reusableCountdown = new CountdownEvent(3);

            // Первый цикл
            Console.WriteLine("Первый цикл обработки:");
            for (int i = 0; i < 3; i++)
            {
                _ = Task.Run(() =>
                {
                    Thread.Sleep(100);
                    reusableCountdown.Signal();
                });
            }
            reusableCountdown.Wait();
            Console.WriteLine("Первый цикл завершен");

            // Сброс для повторного использования
            reusableCountdown.Reset(2); // Сбрасываем и устанавливаем новое значение

            Console.WriteLine("Второй цикл обработки:");
            for (int i = 0; i < 2; i++)
            {
                _ =Task.Run(() =>
                {
                    Thread.Sleep(100);
                    reusableCountdown.Signal();
                });
            }
            reusableCountdown.Wait();
            Console.WriteLine("Второй цикл завершен");

            reusableCountdown.Dispose();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nПравила использования CountdownEvent:");
            Console.WriteLine(" - Идеален для ожидания завершения N независимых операций");
            Console.WriteLine(" - AddCount() и Signal() должны быть сбалансированы");
            Console.WriteLine(" - Всегда использовать try-finally с Signal()");
            Console.WriteLine(" - Reset() позволяет повторно использовать объект");
            Console.ResetColor();
        }

        private class TreeNode
        {
            public int Value { get; }
            public TreeNode Left { get; set; }
            public TreeNode Right { get; set; }

            public TreeNode(int value) => Value = value;
        }
    }
}