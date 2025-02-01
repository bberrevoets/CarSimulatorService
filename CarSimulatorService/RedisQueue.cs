using Prometheus;
using Serilog;
using StackExchange.Redis;
using ILogger = Serilog.ILogger;

namespace CarSimulatorService;

public class RedisQueue : IRedisQueue
{
    private readonly IDatabase _db;
    private readonly ILogger _logger = Log.ForContext<RedisQueue>();

    private readonly string _streamKey;
    private readonly int _trimIntervalSeconds;

    public RedisQueue(CarSimulationSettings settings)
    {
        _streamKey = settings.RedisStreamKey;
        _trimIntervalSeconds = settings.TrimIntervalSeconds;
        try
        {
            _logger.Information("Connecting to Redis at {RedisConnection}...", settings.RedisConnection);
            var redis = ConnectionMultiplexer.Connect(settings.RedisConnection);
            _db = redis.GetDatabase();
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "🚨 Critical: Unable to connect to Redis at {RedisConnection}!",
                settings.RedisConnection);
            throw; // Ensure the service fails to start if redis is unavailable
        }

        // Start background monitoring and trimming tasks
        Task.Run(async () => await MonitorQueueSize());
        Task.Run(async () => await PeriodicTrim());
    }

    private async Task MonitorQueueSize()
    {
        while (true)
        {
            try
            {
                var queueSize = await _db.StreamLengthAsync(_streamKey);
                _queueSizeMetric.Set(queueSize);
                _logger.Information("Redis queue size: {QueueSize}", queueSize);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve Redis queue size.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        // ReSharper disable once FunctionNeverReturns
    }

    public async Task EnqueueMessageAsync(string message)
    {
        try
        {
            var messageId = await _db.StreamAddAsync(_streamKey, [new NameValueEntry("message", message)]);
            _logger.Information("Streamed message [{messageId}]: {message}", messageId, message);
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "🚨 Critical: Failed to enqueue message to Redis stream {StreamKey}!", _streamKey);
        }
    }

    public async Task<StreamEntry[]?> ReadMessagesAsync(string consumerGroup, string consumerName, int batchSize = 10)
    {
        try
        {
            var messages = await _db.StreamReadGroupAsync(_streamKey, consumerGroup, consumerName, ">", batchSize);
            return messages.Length > 0 ? messages : null;
        }
        catch (RedisException ex)
        {
            _logger.Warning(ex, "Read error Stream {StreamKey}!", _streamKey);
            return null;
        }
    }

    public async Task AcknowledgeMessageAsync(string consumerGroup, string messageId)
    {
        await _db.StreamAcknowledgeAsync(_streamKey, consumerGroup, messageId);
        _logger.Information("✅ Acknowledged message {MessageId}", messageId);
    }

    public async Task CreateConsumerGroupAsync(string consumerGroup)
    {
        try
        {
            await _db.StreamCreateConsumerGroupAsync(_streamKey, consumerGroup, "0-0");
            _logger.Information("Created consumer group: {consumerGroup}", consumerGroup);
        }
        catch (RedisException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            _logger.Information("Consumer group {consumerGroup} already exists.", consumerGroup);
        }
    }

    private async Task PeriodicTrim()
    {
        while (true)
        {
            try
            {
                    var removedCount = await _db.StreamTrimAsync(_streamKey, maxLength: 100);

                    // Log after trimming
                    var afterTrim = await _db.StreamLengthAsync(_streamKey);
                    _trimmedMessagesMetric.Inc(removedCount);
                    _logger.Information("Trimmed {RemovedCount} messages from Redis stream.", removedCount);
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "🚨 Critical: Failed to trim Redis stream {StreamKey}!", _streamKey);
            }

            await Task.Delay(TimeSpan.FromSeconds(_trimIntervalSeconds));
        }
        // ReSharper disable once FunctionNeverReturns
    }

    #region [ Prometheus Variables ]

    // Prometheus metrics
    private readonly Gauge _queueSizeMetric =
        Metrics.CreateGauge("redis_queue_size", "Current size of Redis stream queue.");

    private readonly Counter _trimmedMessagesMetric =
        Metrics.CreateCounter("redis_trimmed_messages", "Number of messages trimmed from Redis stream.");

    #endregion
}