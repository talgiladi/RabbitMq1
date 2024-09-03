using Microsoft.Extensions.Configuration;

namespace WebQueueModels
{
    public static class Settings
    {
        private const string _workingQueueName = "webhook_queue";
        private static readonly IConfigurationRoot configuration;
        static Settings()
        {
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            Console.WriteLine(configuration == null ? "null" : configuration.ToString());
            if (configuration == null)
            {
                throw new Exception($"configuration section not found");
            }
        }

        public static string WorkingQueueName
        {
            get { return _workingQueueName; }
        }

        public static string QueueUri
        {
            get
            {
                var uri = configuration["RabbitMQUrl"];
                Console.WriteLine($"RabbitMQUrl: {uri}");
                if (uri == null)
                {
                    throw new Exception($"please set the 'RabbitMQUrl' value in config");
                }

                return uri;
            }
        }
    }
}