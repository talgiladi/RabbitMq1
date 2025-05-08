using RabbitMQ.Client;

namespace WebQueueModels
{
    public class QueueManager : IDisposable
    {
        private IConnection? connection;
        private IChannel? channel;
        public async Task<IChannel> CreateMainQueue()
        {
            var rabbitMQUrl = WebQueueModels.Settings.QueueUri;

            var factory = new ConnectionFactory { HostName = rabbitMQUrl };
            connection = await factory.CreateConnectionAsync();
            channel = await connection.CreateChannelAsync();

            //create main queue which is also the dead queue
            await channel.ExchangeDeclareAsync("dlx_exchange", ExchangeType.Direct);
            await channel.QueueDeclareAsync("dlx_queue", true, false, false, null);
            await channel.QueueBindAsync("dlx_queue", "dlx_exchange", "dlx_routing_key");


            await channel.QueueDeclareAsync(queue: WebQueueModels.Settings.WorkingQueueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            //create retries queues that use the dead queue
            for (var i = 1; i < 4; i++)
            {
                var delay = GetDelay(i);
                var arguments = new Dictionary<string, object?>
                {
                    { "x-dead-letter-exchange", "dlx_exchange" },
                    { "x-dead-letter-routing-key", "dlx_routing_key" },
                    { "x-message-ttl", delay } // TTL in milliseconds
                };
                await channel.QueueDeclareAsync(queue: $"{WebQueueModels.Settings.WorkingQueueName}.retry.{delay}",
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
