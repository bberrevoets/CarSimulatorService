using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace CarSimulatorService.Tests;

public class RedisMessageProcessorTests
{
    private readonly Mock<IRedisQueue> _mockRedisQueue;
    private readonly RedisMessageProcessor _processor;
    private readonly TcpListener _tcpListener;

    public RedisMessageProcessorTests()
    {
        var settings = new CarSimulationSettings
        {
            ServerAddress = "127.0.0.1",
            ServerPort = 5001 // Different port to avoid conflicts
        };

        _mockRedisQueue = new Mock<IRedisQueue>();
        var mockTcpClientWrapper = new Mock<ITcpClientWrapper>();

        // Start a fake TCP server
        _tcpListener = new TcpListener(IPAddress.Loopback, settings.ServerPort);
        _tcpListener.Start();

        // Simulate a fake TCP client
        var fakeTcpClient = new TcpClient();
        fakeTcpClient.Connect(IPAddress.Loopback, settings.ServerPort);
        var networkStream = fakeTcpClient.GetStream(); // Real NetworkStream

        // Mock the wrapper to return the real stream
        mockTcpClientWrapper.Setup(t => t.IsConnected).Returns(true);
        mockTcpClientWrapper.Setup(t => t.ConnectAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);
        mockTcpClientWrapper.Setup(t => t.GetStream()).Returns(networkStream);

        _processor = new RedisMessageProcessor(new NullLogger<RedisMessageProcessor>(), settings,
            _mockRedisQueue.Object, mockTcpClientWrapper.Object);
    }

    [Fact]
    public async Task ProcessQueue_Should_Send_Message_To_Server()
    {
        // Arrange: Mock Redis returning a message
        var testMessage = new StreamEntry("12345-0", [new NameValueEntry("message", "Test Data")]);
        _mockRedisQueue.Setup(r => r.ReadMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync([testMessage]);

        // Start listening for a client connection asynchronously
        var serverTask = Task.Run(async () =>
        {
            var serverClient = await _tcpListener.AcceptTcpClientAsync();
            await using var serverStream = serverClient.GetStream();
            using var reader = new StreamReader(serverStream, Encoding.UTF8);

            // Read one line instead of waiting indefinitely
            return await reader.ReadLineAsync();
        });

        // Act: Process queue (sends message over TCP)
        await _processor.ProcessQueue();

        // Wait for the server to receive the message
        var receivedData = await serverTask;

        // Assert: Verify data was received
        Assert.Contains("Test Data", receivedData);
    }
}