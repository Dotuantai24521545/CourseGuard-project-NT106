using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CourseGuard.Backend.Services.Monitoring
{
    public sealed class TcpScreenMonitorService : IDisposable
    {
        public static readonly TcpScreenMonitorService Instance = new();

        private TcpListener? _listener;
        private bool _isRunning;
        private CancellationTokenSource? _cts;

        public event EventHandler<ScreenFrameReceivedEventArgs>? FrameReceived;
        public event EventHandler<ScreenMonitorStatusEventArgs>? StatusChanged;

        private TcpScreenMonitorService() { }

        public void StartListening()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();

            _listener = new TcpListener(IPAddress.Any, ScreenStreamProtocol.DefaultPort);
            _listener.Start();

            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener!.AcceptTcpClientAsync(cancellationToken);
                    StatusChanged?.Invoke(this, new ScreenMonitorStatusEventArgs($"Đã kết nối Client: {client.Client.RemoteEndPoint}"));
                    _ = Task.Run(() => ReadClientAsync(client, cancellationToken));
                }
                catch (OperationCanceledException) { break; }
                catch (Exception) { /* log or ignore */ }
            }
        }

        private async Task ReadClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            {
                try
                {
                    await using NetworkStream stream = client.GetStream();
                    var header = new byte[ScreenStreamProtocol.HeaderLength];
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await stream.ReadExactlyAsync(header, cancellationToken);
                        if (!ScreenStreamProtocol.TryReadHeader(header, out byte frameType, out int frameExamId, out int frameStudentId, out int frameAttemptId, out _, out int length))
                            break;

                        var payload = new byte[length];
                        await stream.ReadExactlyAsync(payload, cancellationToken);
                        
                        FrameReceived?.Invoke(this, new ScreenFrameReceivedEventArgs(frameType, frameExamId, frameStudentId, frameAttemptId, payload));
                    }
                }
                catch
                {
                    StatusChanged?.Invoke(this, new ScreenMonitorStatusEventArgs("Mất kết nối với một Client"));
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _isRunning = false;
        }
    }
}
