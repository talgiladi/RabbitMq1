using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Queues
{
    public class Program
    {
        public static async Task Main()
        {
            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<QueueConsumerService>();
                })
                .Build();

            await host.RunAsync();
        }
    }

    public class QueueConsumerService : BackgroundService, IDisposable
    {
        private readonly IModel workingChannel;
        private IModel exchangeChannel;
        private readonly int MaxRetries = 3;
        
        private readonly WebQueueModels.QueueManager queueManager;
        public QueueConsumerService()
        {
            queueManager = new WebQueueModels.QueueManager();
            workingChannel = queueManager.CreateMainQueue();
            workingChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            //{
            //    var factory = new ConnectionFactory { HostName = rabbitMQUrl };
            //    var connection = factory.CreateConnection();
            //    //register to failed queue
            //    exchangeChannel = connection.CreateModel();
            //    exchangeChannel.ExchangeDeclare(ExchangeQueueName, "direct", true);
            //    exchangeChannel.QueueDeclare(queue: ExchangeQueueName,
            //                 durable: true,
            //                 exclusive: false,
            //                 autoDelete: false,
            //                 arguments: null);
            //    exchangeChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
            //}

            //  CreateDeadLetterExchange();
            Console.WriteLine(" [*] Waiting for messages.");

        }

        //private void CreateExchange()
        //{
        //    var factory = new ConnectionFactory() { HostName = rabbitMQUrl };
        //    using (var connection = factory.CreateConnection())
        //    using (var channel = connection.CreateModel())
        //    {
        //        // Declare the Dead Letter Exchange and Queue
        //        channel.ExchangeDeclare("dlx_exchange", ExchangeType.Direct);
        //        channel.QueueDeclare("dlx_queue", true, false, false, null);
        //        channel.QueueBind("dlx_queue", "dlx_exchange", "dlx_routing_key");

        //        // Declare the original queue with DLX settings
        //        var arguments = new Dictionary<string, object>
        //    {
        //        { "x-dead-letter-exchange", "dlx_exchange" },
        //        { "x-dead-letter-routing-key", "dlx_routing_key" },
        //        { "x-message-ttl", 60000 } // TTL in milliseconds
        //    };
        //        channel.QueueDeclare(WorkingQueueName, true, false, false, arguments);

        //        // Consumer for the DLX queue
        //        var consumer = new EventingBasicConsumer(channel);
        //        consumer.Received += (model, ea) =>
        //        {
        //            var body = ea.Body.ToArray();
        //            var message = Encoding.UTF8.GetString(body);
        //            Console.WriteLine("Received expired message: {0}", message);
        //        };
        //        channel.BasicConsume("dlx_queue", true, consumer);

        //        Console.WriteLine("Listening for expired messages. Press [enter] to exit.");
        //        Console.ReadLine();
        //    }
        //}

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            {
                var consumer = new EventingBasicConsumer(workingChannel);
                consumer.Received += (model, ea) =>
                {
                    Console.WriteLine("got live message");
                    HandleMessageSafely(ea);
                };
                workingChannel.BasicConsume(queue: WebQueueModels.Settings.WorkingQueueName, autoAck: false, consumer: consumer);
            }
            {
                var exchangeConsumer = new EventingBasicConsumer(workingChannel);
                exchangeConsumer.Received += (model, ea) =>
                {
                    Console.WriteLine("got exchange message");
                    HandleMessageSafely(ea);
                };
                workingChannel.BasicConsume(queue: "dlx_queue", autoAck: false, consumer: exchangeConsumer);
            }
            return Task.CompletedTask;
        }

        private void HandleMessageSafely(BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                var id = Guid.NewGuid().ToString();
                Console.WriteLine($" [x] Received {message}. {id}");
                if (message.Contains("crash"))
                {
                    throw new Exception("crashed!");
                }
                workingChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                Console.WriteLine($" [x] Done {message}. {id}");
            }
            catch (Exception e1)
            {
                Console.WriteLine($"exception: {e1.Message}");
                RetryMessage(message, ea);
            }
        }

        private static int DeathCount(BasicDeliverEventArgs ea)
        {
            var retries = ea.BasicProperties.Headers?.ContainsKey("x-retries") == true
                   ? (int)ea.BasicProperties.Headers["x-retries"]
                   : 0;

            return retries;
        }

        private void RetryMessage(string message, BasicDeliverEventArgs ea)
        {
            var retries = DeathCount(ea);
            Console.WriteLine($"retries: {retries}");
            retries++;
            if (retries > MaxRetries)
            {
                // Handle message discard logic here
                NotifyMessageDiscarded(message, ea);
            }
            else
            {

                CreateRetryQueue(message, retries);
            }

            workingChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
        }

        private void CreateRetryQueue(string message, int retries)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(message);
                var delay = WebQueueModels.QueueManager.GetDelay(retries);
               
                string queueName = $"{WebQueueModels.Settings.WorkingQueueName}.retry.{delay}";// $"{WorkingQueueName}.retry.{delay}";
                var factory = new ConnectionFactory { HostName = WebQueueModels.Settings.QueueUri };
                var connection = factory.CreateConnection();
                var channel = connection.CreateModel();
                var arguments = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "dlx_exchange" },
                { "x-dead-letter-routing-key", "dlx_routing_key" },
                { "x-message-ttl", delay } // TTL in milliseconds
            };
               

                channel.QueueDeclare(queue: queueName,
                             durable: true,
                             exclusive: false,
                             autoDelete: false,
                             arguments: arguments);
                channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                Console.WriteLine($"created queue {queueName}");
                var properties = channel.CreateBasicProperties();
                properties.Expiration = delay.ToString();
                properties.Headers = new Dictionary<string, object> { { "x-retries", retries } };
                properties.Persistent = true;


                channel.BasicPublish(exchange: string.Empty,
                                    routingKey: queueName,
                                    basicProperties: properties,
                                    body: body);
            }
            catch (Exception e1)
            {
                Console.WriteLine($"error creating crash queue {e1.Message} {e1.StackTrace}");
            }


        }

        private void NotifyMessageDiscarded(string message, BasicDeliverEventArgs ea)
        {
            // Logic to notify when a message is discarded
            //_channel.BasicReject(ea.DeliveryTag, false);
            Console.WriteLine($"Message discarded: {message}");
        }

        public override void Dispose()
        {
            try
            {
                workingChannel?.Close();
            }
            catch { }

            try
            {
                exchangeChannel?.Close();
            }
            catch { }

            try
            {
                queueManager?.Dispose();
            }
            catch { }
            base.Dispose();
        }
    }
}