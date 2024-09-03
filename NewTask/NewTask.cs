using System.Text;
using RabbitMQ.Client;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration;

internal class Program
{
    private static int index;
    private static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        Console.WriteLine(configuration ==null ? "null" : configuration.ToString());
        var rabbitMQUrl = configuration["RabbitMQUrl"];
        Console.WriteLine(rabbitMQUrl);
        var factory = new ConnectionFactory { HostName = rabbitMQUrl };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "hello",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);
        int index = 0;
        while (true)
        {
            string message = $"#{index++} {DateTime.UtcNow}";
            var body = Encoding.UTF8.GetBytes(message);

            channel.BasicPublish(exchange: string.Empty,
                                routingKey: "hello",
                                basicProperties: null,
                                body: body);
            Console.WriteLine($" [x] Sent {message}");
            Thread.Sleep(4000);
        }
       
    }
}