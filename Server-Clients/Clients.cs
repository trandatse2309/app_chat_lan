using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LAN_Network_Chat_App
{
    class Clients
    {
        public string ServerIp { get; }
        public int Port { get; }
        public string Name { get; }

        private TcpClient? _client;
        private NetworkStream? _stream;
        private StreamReader? _reader;
        private bool _isConnected = false;

        public event Action<string>? OnMessageReceived;
        public event Action<string>? OnUserJoined;
        public event Action<string>? OnUserLeft;
        public event Action<string>? OnRoomInfo;
        public event Action<string>? OnUsersInfo;
        public event Action? OnNameTaken;
        public event Action? OnDisconnected;

        public Clients(string serverIp, int port, string name)
        {
            this.ServerIp = serverIp;
            this.Port = port;
            this.Name = name;
        }

        public async Task ConnectAsync()
        {
            _client = new TcpClient();
            await _client.ConnectAsync(ServerIp, Port);
            _stream = _client.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _isConnected = true;

            await SendRawAsync($"NAME:{Name}");

            _ = Task.Run(() => ReceiveMessagesAsync());
        }

        public async Task SendMessageAsync(string message)
        {
            if (!_isConnected || _stream == null) return;
            await SendRawAsync($"MSG:{message}");
        }

        public async Task SendNewNameAsync(string newName)
        {
            if (_stream == null) return;
            await SendRawAsync($"NAME:{newName}");
        }

        private async Task SendRawAsync(string raw)
        {
            if (_stream == null) return;
            byte[] data = Encoding.UTF8.GetBytes(raw + "\n");
            await _stream.WriteAsync(data, 0, data.Length);
        }

        private async Task ReceiveMessagesAsync()
        {
            try
            {
                while (_isConnected && _reader != null)
                {
                    // ReadLineAsync đọc đúng 1 dòng (1 message) mỗi lần
                    string? line = await _reader.ReadLineAsync();
                    if (line == null) break;

                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.StartsWith("ROOM:"))
                        OnRoomInfo?.Invoke(line.Substring(5));
                    else if (line.StartsWith("USERS:"))
                        OnUsersInfo?.Invoke(line.Substring(6));
                    else if (line.StartsWith("JOIN:"))
                        OnUserJoined?.Invoke(line.Substring(5));
                    else if (line.StartsWith("LEAVE:"))
                        OnUserLeft?.Invoke(line.Substring(6));
                    else if (line.StartsWith("MSG:"))
                        OnMessageReceived?.Invoke(line.Substring(4));
                    else if (line == "NAME_TAKEN")
                        OnNameTaken?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi nhận tin nhắn: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _reader?.Dispose();
            _stream?.Dispose();
            _client?.Dispose();
            OnDisconnected?.Invoke();
        }
    }
}
