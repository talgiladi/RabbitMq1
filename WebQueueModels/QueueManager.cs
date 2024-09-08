using RabbitMQ.Client;

namespace WebQueueModels
{
    public class QueueManager : IDisposable
    {
        private IConnection? connection;
        private IModel? channel;
        public IModel CreateMainQueue()
        {
            var rabbitMQUrl = WebQueueModels.Settings.QueueUri;

            var factory = new ConnectionFactory { HostName = rabbitMQUrl };
            connection = factory.CreateConnection();
            channel = connection.CreateModel();

            //create main queue which is also the dead queue
            channel.ExchangeDeclare("dlx_exchange", ExchangeType.Direct);
            channel.QueueDeclare("dlx_queue", true, false, false, null);
            channel.QueueBind("dlx_queue", "dlx_exchange", "dlx_routing_key");


            channel.QueueDeclare(queue: WebQueueModels.Settings.WorkingQueueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            //create retries queues that use the dead queue
            for (var i = 1; i < 4; i++)
            {
                var delay = GetDelay(i);
                var arguments = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", "dlx_exchange" },
                    { "x-dead-letter-routing-key", "dlx_routing_key" },
                    { "x-message-ttl", delay } // TTL in milliseconds
                };
                channel.QueueDeclare(queue: $"{WebQueueModels.Settings.WorkingQueueName}.retry.{delay}",
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: arguments);
            }
            return channel;
        }

        public static int GetDelay(int retry)
        {
            return (int)Math.Pow(2, retry + 1) * 1000;
        }

        public void Dispose()
        {
            try
            {
                this.connection?.Dispose();
            }
            catch { }
            try
            {
                this.channel?.Dispose();
            }
            catch { }
        }
    }
}
