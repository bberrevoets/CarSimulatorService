using Moq;
using StackExchange.Redis;

namespace CarSimulatorService.Tests;

public class RedisQueueTests
{
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly RedisQueue _redisQueue;

    public RedisQueueTests()
    {
        var mockMultiplexer = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();

        mockMultiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_mockDatabase.Object);

        var settings = new CarSimulationSettings
        {
            RedisStreamKey = "test_stream",
            TrimIntervalSeconds = 10
        };

        _redisQueue = new RedisQueue(settings);
    }

    [Fact]
    public async Task EnqueueMessageAsync_Should_Add_Message_To_Stream()
    {
        // Arrange
        var message = "Test Message";
        _mockDatabase.Setup(db => db.StreamAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>(), null, null, false, CommandFlags.None))
            .ReturnsAsync("12345-0");

        // Act
        await _redisQueue.EnqueueMessageAsync(message);

        // Assert
        _mockDatabase.Verify(db => db.StreamAddAsync(
            It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>(), null, null, false, CommandFlags.None), Times.Once);
    }
}