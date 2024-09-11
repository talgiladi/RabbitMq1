using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System;
using System.Diagnostics;
using System.Text;
namespace Queues
{
    internal class Program
    {
        private static WebQueueModels.QueueManager? queueManager;
        private static void Main()
        {
            queueManager = new WebQueueModels.QueueManager();
            var channel = queueManager.CreateMainQueue();

            int index = 0;
            //while (true)
            //{
            //    string message = GetMessage($"#{index++} {Guid.NewGuid()}");
            //    var body = Encoding.UTF8.GetBytes(message);
            //    var properties = channel.CreateBasicProperties();
            //    properties.Persistent = true;
            //    channel.BasicPublish(exchange: string.Empty,
            //                        routingKey: WebQueueModels.Settings.WorkingQueueName,
            //                        basicProperties: properties,
            //                        body: body);
            //    Console.WriteLine($" [x] Sent {message}");
            //}
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                string message = new('a', 1_800_000);
                var body = Encoding.UTF8.GetBytes(message);
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                channel.BasicPublish(exchange: string.Empty,
                                    routingKey: WebQueueModels.Settings.WorkingQueueName,
                                    basicProperties: properties,
                                    body: body);
                Console.WriteLine($" [x] Sent #{i}");
            }
            watch.Stop();
            Console.WriteLine($"elapsed {watch.Elapsed.TotalSeconds}");

        }

        static string GetMessage(string defaultMessage)
        {
            Console.WriteLine("Enter your message:");
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                line = defaultMessage;
            }
            return line;
        }
    }
}