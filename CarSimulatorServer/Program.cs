using CarSimulatorServer;

const int port = 5000;
var server = new Server(port);
await server.StartAsync();