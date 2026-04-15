using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LAN_Network_Chat_App
{
    public partial class MainWindow : Window
    {
        private string _mode = "";
        private string _ip = "";
        private string _myName = "";
        private ServerManager? _server;
        private Clients? _client;

        public MainWindow()
        {
            InitializeComponent();
        }

        // ================= START =================

        private async void Host_Click(object sender, RoutedEventArgs e)
        {
            _myName = NameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(_myName))
            {
                MessageBox.Show("Nhập tên của bạn trước!");
                return;
            }

            _mode = "HOST";
            _ip = "127.0.0.1";

            try
            {
                _server = new ServerManager(5000, _myName);
                _client = new Clients(_ip, 5000, _myName);
                RegisterEvents();
                await _client.ConnectAsync();
                SwitchToChat();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tạo phòng: {ex.Message}");
            }
        }

        private async void Join_Click(object sender, RoutedEventArgs e)
        {
            _myName = NameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(_myName))
            {
                MessageBox.Show("Nhập tên của bạn trước!");
                return;
            }

            _ip = IpBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(_ip))
            {
                MessageBox.Show("Nhập IP server!");
                return;
            }

            _mode = "JOIN";

            try
            {
                _client = new Clients(_ip, 5000, _myName);
                RegisterEvents();
                await _client.ConnectAsync();
                SwitchToChat();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể kết nối: {ex.Message}");
            }
        }

        // ================= EVENTS =================

        private void RegisterEvents()
        {
            _client!.OnRoomInfo += (room) =>
                Dispatcher.Invoke(() => RoomLabel.Text = room);

            _client.OnNameTaken += () =>
                Dispatcher.Invoke(async () =>
                {
                    // Hiện dialog yêu cầu nhập tên mới
                    while (true)
                    {
                        string? newName = Microsoft.VisualBasic.Interaction.InputBox(
                            "Tên này đã có người dùng. Nhập tên khác:",
                            "Tên bị trùng",
                            _myName + "_2");

                        newName = newName?.Trim();
                        if (string.IsNullOrWhiteSpace(newName)) continue;

                        _myName = newName;
                        await _client.SendNewNameAsync(_myName);
                        break;
                    }
                });

            _client.OnUsersInfo += (users) =>
                Dispatcher.Invoke(() =>
                {
                    UserList.Children.Clear();
                    AddUserChip(_myName, isSelf: true);
                    foreach (var name in users.Split(','))
                        if (!string.IsNullOrWhiteSpace(name) && name != _myName)
                            AddUserChip(name);
                    UpdateOnlineCount();
                });

            _client.OnUserJoined += (name) =>
                Dispatcher.Invoke(() =>
                {
                    AddUserChip(name);
                    UpdateOnlineCount();
                    AddSystemMessage($"{name} đã tham gia phòng.");
                });

            _client.OnUserLeft += (name) =>
                Dispatcher.Invoke(() =>
                {
                    RemoveUserChip(name);
                    UpdateOnlineCount();
                    AddSystemMessage($"{name} đã rời phòng.");
                });

            _client.OnMessageReceived += (payload) =>
                Dispatcher.Invoke(() =>
                {
                    // payload = "name:text"
                    int sep = payload.IndexOf(':');
                    if (sep < 0) return;
                    string sender = payload.Substring(0, sep);
                    string text = payload.Substring(sep + 1);
                    AddMessage(sender, text, isOwn: false);
                });

            _client.OnDisconnected += () =>
                Dispatcher.Invoke(() => AddSystemMessage("Đã ngắt kết nối khỏi server."));
        }

        // ================= UI SWITCH =================

        private void SwitchToChat()
        {
            StartPanel.Visibility = Visibility.Collapsed;
            ChatPanel.Visibility = Visibility.Visible;

            ModeLabel.Text = _mode;

            if (_mode == "HOST" && _server != null)
            {
                RoomLabel.Text = $"Phòng chat của {_server.HostName}";
                ShowIpButton.Visibility = Visibility.Visible;
                PopulateIpList();
            }

            AddUserChip(_myName, isSelf: true);
            UpdateOnlineCount();
            MessageInput.Focus();
        }

        // ================= IP POPUP =================

        private void PopulateIpList()
        {
            IpList.Children.Clear();

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                    string ip = addr.Address.ToString();
                    string name = ni.Name;

                    // Row: tên mạng — IP (click để copy)
                    var row = new Border
                    {
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(8, 6, 8, 6),
                        Margin = new Thickness(0, 0, 0, 4),
                        Background = new SolidColorBrush(Color.FromRgb(30, 30, 46)),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        ToolTip = "Click để copy"
                    };

                    var content = new StackPanel { Orientation = Orientation.Horizontal };
                    content.Children.Add(new TextBlock
                    {
                        Text = name,
                        Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center,
                        MinWidth = 110
                    });
                    content.Children.Add(new TextBlock
                    {
                        Text = ip,
                        Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    row.Child = content;
                    row.MouseLeftButtonUp += (s, e) =>
                    {
                        Clipboard.SetText(ip);
                        IpPopup.IsOpen = false;
                    };

                    // Hover effect
                    row.MouseEnter += (s, e) =>
                        ((Border)s).Background = new SolidColorBrush(Color.FromRgb(45, 45, 68));
                    row.MouseLeave += (s, e) =>
                        ((Border)s).Background = new SolidColorBrush(Color.FromRgb(30, 30, 46));

                    IpList.Children.Add(row);
                }
            }
        }

        private void ShowIpButton_Click(object sender, RoutedEventArgs e)
        {
            IpPopup.IsOpen = !IpPopup.IsOpen;
        }

        // ================= SEND =================

        private async void Send_Click(object sender, RoutedEventArgs e) => await SendMessage();

        private async void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await SendMessage();
        }

        private async Task SendMessage()
        {
            string msg = MessageInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(msg) || _client == null) return;

            AddMessage(_myName, msg, isOwn: true);
            await _client.SendMessageAsync(msg);
            MessageInput.Clear();
        }

        // ================= UI HELPERS =================

        private void AddMessage(string sender, string text, bool isOwn)
        {
            var container = new StackPanel
            {
                HorizontalAlignment = isOwn ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = new Thickness(isOwn ? 60 : 0, 4, isOwn ? 0 : 60, 4)
            };

            if (!isOwn)
            {
                container.Children.Add(new TextBlock
                {
                    Text = sender,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    FontSize = 11,
                    Margin = new Thickness(4, 0, 0, 2)
                });
            }

            container.Children.Add(new Border
            {
                Background = isOwn
                    ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                    : new SolidColorBrush(Color.FromRgb(45, 45, 68)),
                CornerRadius = new CornerRadius(isOwn ? 12 : 12),
                Padding = new Thickness(12, 8, 12, 8),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap
                }
            });

            ChatList.Children.Add(container);
            ChatScroll.ScrollToBottom();
        }

        private void AddSystemMessage(string text)
        {
            ChatList.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 6)
            });
            ChatScroll.ScrollToBottom();
        }

        private void AddUserChip(string name, bool isSelf = false)
        {
            var chip = new Border
            {
                Tag = name,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 68)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 0, 0, 4),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new Ellipse
                        {
                            Width = 7, Height = 7,
                            Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                            Margin = new Thickness(0, 0, 8, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = isSelf ? $"{name} (bạn)" : name,
                            Foreground = isSelf
                                ? new SolidColorBrush(Color.FromRgb(167, 139, 250))
                                : new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                            FontSize = 12,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };

            UserList.Children.Add(chip);
        }

        private void RemoveUserChip(string name)
        {
            foreach (UIElement el in UserList.Children)
            {
                if (el is Border b && b.Tag?.ToString() == name)
                {
                    UserList.Children.Remove(b);
                    break;
                }
            }
        }

        private void UpdateOnlineCount()
        {
            OnlineCount.Text = UserList.Children.Count.ToString();
        }
    }
}
