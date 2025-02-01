using System.Net.Sockets;

namespace CarSimulatorService;

public class TcpClientWrapper : ITcpClientWrapper
{
    private readonly TcpClient _tcpClient = new();

    public async Task<bool> ConnectAsync(string host, int port)
    {
        try
        {
            await _tcpClient.ConnectAsync(host, port);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public NetworkStream GetStream()
    {
        return _tcpClient.GetStream();
    }

    public bool IsConnected => _tcpClient.Connected;

    public void Close()
    {
        _tcpClient.Close();
    }

    public void Dispose()
    {
        _tcpClient.Dispose();
    }
}