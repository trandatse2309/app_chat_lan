using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LAN_Network_Chat_App
{
    class ServerManager
    {
        public int Port { get; }
        public string HostName { get; }
        public string HostIp { get; }

        // Event báo cho UI khi có người JOIN/LEAVE
        public event Action<string>? OnUserJoined;
        public event Action<string>? OnUserLeft;

        private TcpListener _listener;
        private readonly object _lock = new object();

        // Map: TcpClient → tên người dùng
        private Dictionary<TcpClient, string> _clients = new Dictionary<TcpClient, string>();
        private bool _isRunning = false;

        public ServerManager(int port, string hostName)
        {
            this.Port = port;
            this.HostName = hostName;
            this.HostIp = GetLocalIp();

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _isRunning = true;
            _ = Task.Run(() => AcceptClientsAsync());
        }

        private static string GetLocalIp()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            return "127.0.0.1";
        }

        private async Task AcceptClientsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (Exception ex) when (_isRunning)
                {
                    Console.WriteLine($"Lỗi AcceptClient: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead;
            string clientName = "unknown";

            try
            {
                // Vòng lặp xử lý tên — yêu cầu đổi nếu trùng
                while (true)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) { client.Close(); return; }

                    string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    if (!msg.StartsWith("NAME:")) continue;

                    string requestedName = msg.Substring(5).Trim();

                    bool taken;
                    lock (_lock) { taken = _clients.Values.Contains(requestedName); }

                    if (taken)
                    {
                        SendTo(client, "NAME_TAKEN");
                    }
                    else
                    {
                        clientName = requestedName;
                        lock (_lock) { _clients[client] = clientName; }
                        SendTo(client, "NAME_OK");
                        break;
                    }
                }

                // Gửi thông tin phòng cho client mới
                string roomName = $"Phòng chat của {HostName}";
                SendTo(client, $"ROOM:{roomName}");

                // Gửi danh sách người đang online
                SendTo(client, $"USERS:{GetUserList()}");

                // Báo cho tất cả người khác biết có người mới vào
                BroadcastExcept($"JOIN:{clientName}", client);
                OnUserJoined?.Invoke(clientName);

                Console.WriteLine($"{clientName} đã kết nối.");

                // Vòng lặp nhận tin nhắn
                while (client.Connected)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string raw = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    if (raw.StartsWith("MSG:"))
                    {
                        string text = raw.Substring(4);
                        string formatted = $"MSG:{clientName}:{text}";
                        BroadcastExcept(formatted, client);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi HandleClient: {ex.Message}");
            }
            finally
            {
                lock (_lock) { _clients.Remove(client); }
                BroadcastExcept($"LEAVE:{clientName}", client);
                OnUserLeft?.Invoke(clientName);
                client.Close();
                Console.WriteLine($"{clientName} đã ngắt kết nối.");
            }
        }

        private string GetUserList()
        {
            lock (_lock)
                return string.Join(",", _clients.Values);
        }

        private void SendTo(TcpClient client, string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message + "\n");
                client.GetStream().Write(data, 0, data.Length);
            }
            catch { }
        }

        private void BroadcastExcept(string message, TcpClient except)
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            List<TcpClient> snapshot;
            lock (_lock) { snapshot = _clients.Keys.ToList(); }

            foreach (TcpClient c in snapshot)
            {
                if (c == except) continue;
                try { c.GetStream().Write(data, 0, data.Length); }
                catch { }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
        }
    }
}
