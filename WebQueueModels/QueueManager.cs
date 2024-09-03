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
            channel.ExchangeDeclare(WebQueueModels.Settings.GetDeadMessagesQueue(), ExchangeType.Direct);
            channel.QueueDeclare("dlx_queue", true, false, false, null);
            channel.QueueBind("dlx_queue", WebQueueModels.Settings.GetDeadMessagesQueue(), "dlx_routing_key");

            // Declare the original queue with DLX settings
            var arguments = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", WebQueueModels.Settings.GetDeadMessagesQueue() },
                { "x-dead-letter-routing-key", "dlx_routing_key" },
                { "x-message-ttl", 10000 } // TTL in milliseconds
            };
            channel.QueueDeclare(queue: WebQueueModels.Settings.WorkingQueueName,
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
