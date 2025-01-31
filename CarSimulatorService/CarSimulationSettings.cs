namespace CarSimulatorService;

public class CarSimulationSettings
{
    public string ServerAddress { get; init; } = "127.0.0.1";
    public int ServerPort { get; init; } = 5000;
    public string RedisConnection { get; init; } = "localhost:6379";
    public string RedisStreamKey { get; init; } = "car_simulator_queue";
    public int TrimIntervalSeconds { get; set; } = 300; // Default: 5 minutes
}