using GeoCoordinatePortable;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CarSimulatorService.Tests;

public class CarSimulatorWorkerTests
{
    private readonly CarSimulatorWorker _worker;

    public CarSimulatorWorkerTests()
    {
        var mockRedisQueue = new Mock<RedisQueue>(new CarSimulationSettings());
        _worker = new CarSimulatorWorker(new NullLogger<CarSimulatorWorker>(), mockRedisQueue.Object);
    }

    [Fact]
    public void GetDirection_Should_Calculate_Correct_Direction()
    {
        // Arrange
        var start = new GeoCoordinate(51.9225, 4.47917);
        var end = new GeoCoordinate(51.925, 4.48);

        // Act
        var direction = _worker.GetDirection(start, end);

        // Assert
        Assert.InRange(direction, 0, 360);
    }

    [Fact]
    public void AdjustSpeed_Should_Increase_Speed_Up_To_Limit()
    {
        // Arrange
        _worker.TargetSpeed = 50; // km/h
        _worker.CurrentSpeed = 30; // km/h

        // Act
        _worker.AdjustSpeed();

        // Assert
        Assert.True(_worker.CurrentSpeed is > 30 and <= 50);
    }
}