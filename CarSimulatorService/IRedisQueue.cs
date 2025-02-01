using StackExchange.Redis;

namespace CarSimulatorService;

public interface IRedisQueue
{
    Task EnqueueMessageAsync(string message);
    Task<StreamEntry[]?> ReadMessagesAsync(string consumerGroup, string consumerName, int batchSize = 10);
    Task AcknowledgeMessageAsync(string consumerGroup, string messageId);
    Task CreateConsumerGroupAsync(string consumerGroup);
}