using GeoCoordinatePortable;

namespace CarSimulatorService;

public class CarSimulatorWorker : BackgroundService
{
    private readonly ILogger<CarSimulatorWorker> _logger;

    private readonly IRedisQueue _redisQueue;

    private readonly GeoCoordinate[] _route;
    private double _currentDirection;
    private int _currentPosition;
    public double CurrentSpeed;
    public double TargetSpeed = 50.0;
    private const double AccelerationRate = 2.5; // km/h per second
    private const double DecelerationRate = 4.0; // km/h per second

    private static readonly Random Random = new Random();

    public CarSimulatorWorker(ILogger<CarSimulatorWorker> logger, IRedisQueue redisQueue)
    {
        _logger = logger;
        _redisQueue = redisQueue;

        // Define a simple hardcoded route
        _route =
        [
            new GeoCoordinate(51.9225, 4.47917),
            new GeoCoordinate(51.925, 4.48),
            new GeoCoordinate(51.9275, 4.482),
            new GeoCoordinate(51.93, 4.484)
        ];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            UpdateMovement();
            var message = GenerateMessage();
            await _redisQueue.EnqueueMessageAsync(message);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private void UpdateMovement()
    {
        if (_currentPosition >= _route.Length - 1)
            _currentPosition = 0;

        var currentLocation = _route[_currentPosition];
        var nextLocation = _route[(_currentPosition + 1) % _route.Length];

        double distance = currentLocation.GetDistanceTo(nextLocation);

        AdjustSpeed();

        double travelTime = 10.0;
        double distanceToMove = (CurrentSpeed / 3.6) * travelTime;

        if (distanceToMove >= distance)
        {
            _currentPosition = (_currentPosition + 1) % _route.Length;
        }
        else
        {
            double progressRatio = distanceToMove / distance;
            double newLat = currentLocation.Latitude + (nextLocation.Latitude - currentLocation.Latitude) * progressRatio;
            double newLon = currentLocation.Longitude + (nextLocation.Longitude - currentLocation.Longitude) * progressRatio;

            _route[_currentPosition] = new GeoCoordinate(newLat, newLon);
        }

        _currentDirection = GetDirection(currentLocation, nextLocation);
    }

    public void AdjustSpeed()
    {
        double speedDifference = TargetSpeed - CurrentSpeed;

        if (Math.Abs(speedDifference) < 0.5) return;

        if (speedDifference > 0)
        {
            CurrentSpeed += Math.Min(AccelerationRate, speedDifference);
        }
        else
        {
            CurrentSpeed += Math.Max(-DecelerationRate, speedDifference);
        }
    }

    private string GenerateMessage()
    {
        if (_currentPosition >= _route.Length - 1)
            _currentPosition = 0; // Loop back

        var currentLocation = _route[_currentPosition];
        var nextLocation = _route[(_currentPosition + 1) % _route.Length];

        CurrentSpeed = GetSpeedBetween(currentLocation, nextLocation);
        _currentDirection = GetDirection(currentLocation, nextLocation);
        _currentPosition++;

        var timestamp = DateTime.UtcNow.ToString("o"); // ISO 8601 UTC timestamp
        return
            $"{{\"timestamp\": \"{timestamp}\", \"lat\": {currentLocation.Latitude}, \"lng\": {currentLocation.Longitude}, \"speed\": {CurrentSpeed}, \"direction\": {_currentDirection}}}";
    }

    private double GetSpeedBetween(GeoCoordinate start, GeoCoordinate end)
    {
        var distance = start.GetDistanceTo(end);
        return distance / 1000 * 3.6;
    }

    public double GetDirection(GeoCoordinate start, GeoCoordinate end)
    {
        var deltaX = end.Longitude - start.Longitude;
        var deltaY = end.Latitude - start.Latitude;
        return Math.Atan2(deltaY, deltaX) * (180 / Math.PI);
    }
}