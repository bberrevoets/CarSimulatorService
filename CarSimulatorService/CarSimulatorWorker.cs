using GeoCoordinatePortable;

namespace CarSimulatorService;

public class CarSimulatorWorker : BackgroundService
{
    private readonly ILogger<CarSimulatorWorker> _logger;

    private readonly RedisQueue _redisQueue;

    private readonly GeoCoordinate[] _route;
    private double _currentDirection;
    private int _currentPosition;
    private double _currentSpeed;

    public CarSimulatorWorker(ILogger<CarSimulatorWorker> logger, RedisQueue redisQueue)
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
            var message = GenerateMessage();
            await _redisQueue.EnqueueMessageAsync(message);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private string GenerateMessage()
    {
        if (_currentPosition >= _route.Length - 1)
            _currentPosition = 0; // Loop back

        var currentLocation = _route[_currentPosition];
        var nextLocation = _route[(_currentPosition + 1) % _route.Length];

        _currentSpeed = GetSpeedBetween(currentLocation, nextLocation);
        _currentDirection = GetDirection(currentLocation, nextLocation);
        _currentPosition++;

        var timestamp = DateTime.UtcNow.ToString("o"); // ISO 8601 UTC timestamp
        return
            $"{{\"timestamp\": \"{timestamp}\", \"lat\": {currentLocation.Latitude}, \"lng\": {currentLocation.Longitude}, \"speed\": {_currentSpeed}, \"direction\": {_currentDirection}}}";
    }

    private string SimulateCarMovement()
    {
        if (_currentPosition >= _route.Length - 1)
            _currentPosition = 0;

        var currentLocation = _route[_currentPosition];
        var nextLocation = _route[(_currentPosition + 1) % _route.Length];

        _currentSpeed = GetSpeedBetween(currentLocation, nextLocation);
        _currentDirection = GetDirection(currentLocation, nextLocation);

        _currentPosition++;

        return
            $"{{\"lat\": {currentLocation.Latitude}, \"lng\": {currentLocation.Longitude}, \"speed\": {_currentSpeed}, \"direction\": {_currentDirection}}}";
    }

    private double GetSpeedBetween(GeoCoordinate start, GeoCoordinate end)
    {
        var distance = start.GetDistanceTo(end);
        return distance / 1000 * 3.6;
    }

    private double GetDirection(GeoCoordinate start, GeoCoordinate end)
    {
        var deltaX = end.Longitude - start.Longitude;
        var deltaY = end.Latitude - start.Latitude;
        return Math.Atan2(deltaY, deltaX) * (180 / Math.PI);
    }
}