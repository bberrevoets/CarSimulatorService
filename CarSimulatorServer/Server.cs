using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CarSimulatorServer;

public class Server(int port)
{
    public async Task StartAsync()
    {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"🚀 Server is listening on port {port}...");

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        Console.WriteLine($"✅ New client connected: {client.Client.RemoteEndPoint}");

        await using var stream = client.GetStream();
        var buffer = new byte[1024];

        while (client.Connected)
        {
            int bytesRead;
            try
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            }
            catch
            {
                Console.WriteLine("❌ Client disconnected.");
                break;
            }

            if (bytesRead == 0) break; // Client disconnected

            var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"📥 Received: {message}");
        }

        client.Close();
        Console.WriteLine("❌ Client connection closed.");
    }
}