using System.Net.Sockets;
using System.Text;

namespace CarSimulatorService;

public class RedisMessageProcessor(
    ILogger<RedisMessageProcessor> logger,
    CarSimulationSettings settings,
    RedisQueue redisQueue) : BackgroundService
{
    private const string ConsumerGroup = "car_consumers";
    private readonly string _consumerName = $"consumer-{Guid.NewGuid()}";

    private NetworkStream? _networkStream;
    private TcpClient? _tcpClient;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await redisQueue.CreateConsumerGroupAsync(ConsumerGroup);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_tcpClient is not { Connected: true }) await TryConnectAsync();

            if (_tcpClient?.Connected == true && _networkStream != null) await ProcessQueue();

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessQueue()
    {
        var messages = await redisQueue.ReadMessagesAsync(ConsumerGroup, _consumerName, 10);

        if (messages == null) return;

        foreach (var message in messages)
        {
            var jsonMessage = message.Values[0].Value.ToString();
            var data = Encoding.UTF8.GetBytes(jsonMessage);

            if (_networkStream == null || !_tcpClient!.Connected)
            {
                await TryConnectAsync();
                if (_networkStream == null) return;
            }

            logger.LogInformation($"Sending: {jsonMessage}");
            await _networkStream.WriteAsync(data, 0, data.Length);
            await redisQueue.AcknowledgeMessageAsync(ConsumerGroup, message.Id!);
        }
    }

    private async Task TryConnectAsync()
    {
        try
        {
            logger.LogInformation($"Attempting to connect to {settings.ServerAddress}:{settings.ServerPort}...");
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(settings.ServerAddress, settings.ServerPort);
            _networkStream = _tcpClient.GetStream();
            logger.LogInformation("Connected to server.");
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Failed to connect: {ex.Message}");
            _tcpClient?.Dispose();
            _tcpClient = null;
            _networkStream = null;
        }
    }
}