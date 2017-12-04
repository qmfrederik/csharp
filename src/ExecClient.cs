using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace k8s
{
    public class ExecClient : IDisposable
    {
        private const int StdInStreamIndex = 0;
        private const int StdOutStreamIndex = 1;
        private const int StdErrStreamIndex = 2;

        private readonly ClientWebSocket socket;
        private readonly CancellationTokenSource cts;
        private Task runLoop;

        public ExecClient(ClientWebSocket socket)
        {
            this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
            this.cts = new CancellationTokenSource();
        }

        public event EventHandler ConnectionClosed;
        public event EventHandler<string> StandardOutputReceived;
        public event EventHandler<string> StandardErrorReceived;

        public void Start()
        {
            if (this.runLoop == null)
            {
                this.runLoop = this.RunLoop(this.cts.Token);
            }
        }

        public void Stop(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this.runLoop != null)
            {
                this.cts.Cancel();
                this.runLoop.Wait(cancellationToken);
            }
        }

        public void Dispose()
        {
            this.Stop();
        }

        public async Task Write(string command, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Send:
            //  - [uint32: stream index = 0 for stdin]
            //  - [byte[]: command as UTF8 value]
            var commandLength = Encoding.UTF8.GetByteCount(command);
            var buffer = ArrayPool<byte>.Shared.Rent(commandLength + 4);

            try
            {
                // The first 4 bytes represent the stream index. For stdin this is 0
                buffer[0] = 0;
                buffer[1] = 0;
                buffer[2] = 0;
                buffer[3] = 0;

                // Copy the command to the buffer, starting at offset 4
                Encoding.UTF8.GetBytes(command, 0, command.Length, buffer, 4);

                ArraySegment<byte> segment = new ArraySegment<byte>(buffer, 0, commandLength + 4);
                await this.socket.SendAsync(segment, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        protected async Task RunLoop(CancellationToken cancellationToken)
        {
            // Get a 1KB buffer
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
            var segment = new ArraySegment<byte>(buffer);

            while (!cancellationToken.IsCancellationRequested && this.socket.CloseStatus == null)
            {
                // We always get data in this format:
                // [stream index] (1 for stdout, 2 for stderr)
                // [payload]
                var result = await this.socket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);

                // Ignore empty messages
                if (result.Count < 2)
                {
                    continue;
                }

                var streamType = buffer[0];


                var value = Encoding.UTF8.GetString(buffer, 1, result.Count - 1);

                switch (streamType)
                {
                    case StdOutStreamIndex:
                        this.StandardOutputReceived?.Invoke(this, value);
                        break;

                    case StdErrStreamIndex:
                        this.StandardErrorReceived?.Invoke(this, value);
                        break;
                }
            }

            this.ConnectionClosed?.Invoke(this, EventArgs.Empty);
            this.runLoop = null;
        }
    }
}
