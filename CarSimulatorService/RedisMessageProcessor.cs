using System.Net.Sockets;
using System.Text;

namespace CarSimulatorService;

public class RedisMessageProcessor(
    ILogger<RedisMessageProcessor> logger,
    CarSimulationSettings settings,
    IRedisQueue redisQueue,
    ITcpClientWrapper tcpClientWrapper)
    : BackgroundService
{
    private const string ConsumerGroup = "car_consumers";
    private readonly string _consumerName = $"consumer-{Guid.NewGuid()}";

    private NetworkStream? _networkStream;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await redisQueue.CreateConsumerGroupAsync(ConsumerGroup);

        while (!stoppingToken.IsCancellationRequested)
        {
            switch (tcpClientWrapper.IsConnected)
            {
                case false:
                    await TryConnectAsync();
                    break;
                case true when _networkStream != null:
                    await ProcessQueue();
                    break;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    public async Task ProcessQueue()
    {
        var messages = await redisQueue.ReadMessagesAsync(ConsumerGroup, _consumerName);

        if (messages == null)
        {
            logger.LogWarning("No new messages found in Redis stream.");
            return;
        }

        foreach (var message in messages)
        {
            var jsonMessage = message.Values[0].Value.ToString();
            var data = Encoding.UTF8.GetBytes(jsonMessage);

            if (_networkStream == null || !tcpClientWrapper.IsConnected)
            {
                await TryConnectAsync();
                if (_networkStream == null) return;
            }

            try
            {
                logger.LogInformation("Sending: {JsonMessage}", jsonMessage);
                await _networkStream.WriteAsync(data, 0, data.Length);
                await redisQueue.AcknowledgeMessageAsync(ConsumerGroup, message.Id!);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Sending Failed, trying to reconnect...");
                
                await TryConnectAsync();
                if (_networkStream == null) return;
            }
        }
    }

    private async Task TryConnectAsync()
    {
        try
        {
            logger.LogInformation($"Attempting to connect to {settings.ServerAddress}:{settings.ServerPort}...");
            if (await tcpClientWrapper.ConnectAsync(settings.ServerAddress, settings.ServerPort))
            {
                _networkStream = tcpClientWrapper.GetStream();
                logger.LogInformation("Connected to server.");
            }
            else
            {
                _networkStream = null;
                logger.LogWarning("Failed to connect.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Failed to connect: {ex.Message}");
            tcpClientWrapper.Dispose();
            _networkStream = null;
        }
    }
}