using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration;

public class Program
{
    public static async Task Main(string[] args)
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

public class QueueConsumerService : BackgroundService
{
    private readonly IModel _channel;

    public QueueConsumerService()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var rabbitMQUrl = configuration["RabbitMQUrl"];
        var factory = new ConnectionFactory { HostName = rabbitMQUrl };
        var connection = factory.CreateConnection();
        _channel = connection.CreateModel();
        _channel.QueueDeclare(queue: "hello",
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

        Console.WriteLine(" [*] Waiting for messages.");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var id = Guid.NewGuid().ToString();
            Console.WriteLine($" [x] Received {message}. {id}");
            int dots = message.Split('.').Length - 1;
            Thread.Sleep(dots * 1000);
            Console.WriteLine($" [x] Done. {id}");
        };
        _channel.BasicConsume(queue: "hello", autoAck: true, consumer: consumer);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel.Close();
        base.Dispose();
    }
}

