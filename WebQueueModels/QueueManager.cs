using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebQueueModels
{
    public class QueueManager : IDisposable
    {
        private IConnection? connection;
        private IModel? channel;
        public IModel CreateDeadMessagesQueue()
        {
            var rabbitMQUrl = WebQueueModels.Settings.QueueUri;

            var factory = new ConnectionFactory { HostName = rabbitMQUrl };
            connection = factory.CreateConnection();
            channel = connection.CreateModel();
            channel.ExchangeDeclare("dlx_exchange", ExchangeType.Direct);
            channel.QueueDeclare("dlx_queue", true, false, false, null);
            channel.QueueBind("dlx_queue", "dlx_exchange", "dlx_routing_key");

            // Declare the original queue with DLX settings
            var arguments = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "dlx_exchange" },
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
