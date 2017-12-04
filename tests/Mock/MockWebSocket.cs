using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace k8s.tests.Mock
{
    internal class MockWebSocket : WebSocket
    {
        private WebSocketCloseStatus? closeStatus;
        private WebSocketState state;

        public override WebSocketCloseStatus? CloseStatus => this.closeStatus;

        public override WebSocketState State => this.state;

        public override string CloseStatusDescription => throw new NotImplementedException();

        public override string SubProtocol => throw new NotImplementedException();

        public Queue<ArraySegment<byte>> PacketsToSend { get; } = new Queue<ArraySegment<byte>>();
        public Collection<ArraySegment<byte>> PacketsReceived { get; } = new Collection<ArraySegment<byte>>();

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (this.PacketsToSend.Count > 0)
            {
                var nextPacket = this.PacketsToSend.Dequeue();
                nextPacket.CopyTo(buffer);

                return Task.FromResult(
                    new WebSocketReceiveResult(nextPacket.Count, WebSocketMessageType.Binary, false));
            }
            else
            {
                this.closeStatus = WebSocketCloseStatus.NormalClosure;
                this.state = WebSocketState.Closed;
                return Task.FromResult(
                    new WebSocketReceiveResult(0, WebSocketMessageType.Binary, true));
            }
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            this.PacketsReceived.Add(buffer);
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
        }

        public override void Abort()
        {
            throw new NotImplementedException();
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
