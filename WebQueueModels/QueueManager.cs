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


            // Declare the original queue with DLX settings
            var arguments = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "dlx_exchange" },
                { "x-dead-letter-routing-key", "dlx_routing_key" },
                { "x-message-ttl", 10000 } // TTL in milliseconds
            };
            channel.QueueDeclare(queue: $"{WebQueueModels.Settings.WorkingQueueName}.retry.10000",
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: arguments);
         
            return channel;
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
