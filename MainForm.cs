using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RADLER.Launcher;

public sealed class MainForm : Form
{
    private readonly TextBox gamePath = new() { Width = 420, PlaceholderText = @"Path to Game.exe" };
    private readonly TextBox peerIp = new() { Width = 160, Text = "127.0.0.1" };
    private readonly NumericUpDown port = new() { Width = 90, Minimum = 1024, Maximum = 65535, Value = 27025 };
    private readonly Label status = new() { AutoSize = true, Text = "Not connected" };
    private readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private UdpClient? udp;
    private CancellationTokenSource? cts;
    private bool isHost;

    public MainForm()
    {
        Text = "RADLER Multiplayer Launcher v0.1";
        MinimumSize = new Size(650, 470);
        Size = new Size(760, 540);
        StartPosition = FormStartPosition.CenterScreen;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 3, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        AddRow(layout, 0, "Game executable:", gamePath);
        var browse = new Button { Text = "Browse…" };
        browse.Click += (_, _) => BrowseGame();
        layout.Controls.Add(browse, 2, 0);

        AddRow(layout, 1, "Friend IP:", peerIp);
        AddRow(layout, 2, "UDP port:", port);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var host = new Button { Text = "HOST — Create room", AutoSize = true };
        var connect = new Button { Text = "CLIENT — Connect", AutoSize = true };
        var launch = new Button { Text = "Launch Game.exe", AutoSize = true };
        var stop = new Button { Text = "Disconnect", AutoSize = true };
        host.Click += async (_, _) => await StartHost();
        connect.Click += async (_, _) => await ConnectClient();
        launch.Click += (_, _) => LaunchGame();
        stop.Click += (_, _) => StopNetwork();
        buttons.Controls.AddRange(new Control[] { host, connect, launch, stop });
        layout.Controls.Add(buttons, 0, 3);
        layout.SetColumnSpan(buttons, 3);

        layout.Controls.Add(new Label { Text = "Status:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        layout.Controls.Add(status, 1, 4);
        layout.Controls.Add(log, 0, 5);
        layout.SetColumnSpan(log, 3);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        FormClosing += (_, _) => StopNetwork();
        Log("RADLER v0.1 ready. This prototype tests LAN messaging only; it does not yet sync Spider-Man inside the game.");
    }

    private static void AddRow(TableLayoutPanel panel, int row, string label, Control control)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        panel.Controls.Add(control, 1, row);
    }

    private void BrowseGame()
    {
        using var dialog = new OpenFileDialog { Filter = "Game executable (Game.exe)|Game.exe|Executable files (*.exe)|*.exe", Title = "Select The Amazing Spider-Man 2 Game.exe" };
        if (dialog.ShowDialog(this) == DialogResult.OK) gamePath.Text = dialog.FileName;
    }

    private async Task StartHost()
    {
        StopNetwork();
        try
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Any, (int)port.Value));
            isHost = true;
            cts = new CancellationTokenSource();
            status.Text = $"Hosting UDP {port.Value}";
            Log($"Room created. Give your friend this PC's LAN IPv4 address and port {port.Value}.");
            await ReceiveLoop(cts.Token);
        }
        catch (Exception ex) { Log("Host error: " + ex.Message); }
    }

    private async Task ConnectClient()
    {
        StopNetwork();
        try
        {
            var address = IPAddress.Parse(peerIp.Text.Trim());
            udp = new UdpClient(0);
            isHost = false;
            cts = new CancellationTokenSource();
            status.Text = "Connecting…";
            _ = ReceiveLoop(cts.Token);
            var endpoint = new IPEndPoint(address, (int)port.Value);
            for (int i = 0; i < 5 && !cts.IsCancellationRequested; i++)
            {
                await Send("RADLER_HELLO", endpoint);
                await Task.Delay(700);
            }
            Log("Connection attempts sent. Check that both PCs are on the same LAN and Windows Firewall allows RADLER UDP.");
        }
        catch (Exception ex) { Log("Client error: " + ex.Message); }
    }

    private async Task ReceiveLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && udp != null)
            {
                var result = await udp.ReceiveAsync(token);
                var message = Encoding.UTF8.GetString(result.Buffer);
                if (message == "RADLER_HELLO")
                {
                    Log($"HELLO received from {result.RemoteEndPoint}");
                    await Send("RADLER_WELCOME", result.RemoteEndPoint);
                    status.Text = "Peer discovered: " + result.RemoteEndPoint.Address;
                }
                else if (message == "RADLER_WELCOME")
                {
                    Log($"WELCOME received from {result.RemoteEndPoint}");
                    status.Text = "Connected to " + result.RemoteEndPoint.Address;
                }
                else Log($"Received: {message}");
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { if (!IsDisposed) Log("Network: " + ex.Message); }
    }

    private Task Send(string message, IPEndPoint endpoint)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        return udp!.SendAsync(bytes, endpoint).AsTask();
    }

    private void StopNetwork()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
        udp?.Close();
        udp?.Dispose();
        udp = null;
        if (!IsDisposed) status.Text = "Not connected";
    }

    private void LaunchGame()
    {
        var path = gamePath.Text.Trim().Trim('"');
        if (!System.IO.File.Exists(path))
        {
            MessageBox.Show(this, "Select a valid Game.exe first.", "RADLER", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { WorkingDirectory = System.IO.Path.GetDirectoryName(path)! });
            Log("Game.exe launched.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Launch error"); }
    }

    private void Log(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action(() => Log(message))); return; }
        log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
