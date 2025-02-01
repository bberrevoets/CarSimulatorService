using System.Net.Sockets;

namespace CarSimulatorService;

public interface ITcpClientWrapper
{
    bool IsConnected { get; }
    Task<bool> ConnectAsync(string host, int port);
    NetworkStream GetStream();
    void Close();
    void Dispose();
}