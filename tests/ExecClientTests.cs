using k8s.tests.Mock;
using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using Xunit;

namespace k8s.tests
{
    public class ExecClientTests
    {
        [Fact]
        public void ReadWriteTest()
        {
            // Queue two packets: stdout and stderr output.
            MockWebSocket webSocket = new MockWebSocket();
            webSocket.PacketsToSend.Enqueue(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes("\x01Hello standard output")));
            webSocket.PacketsToSend.Enqueue(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes("\x02Hello standard error")));

            Collection<string> standardOutputMessages = new Collection<string>();
            Collection<string> standardErrorMessages = new Collection<string>();

            ManualResetEvent closedEvent = new ManualResetEvent(false);

            ExecClient client = new ExecClient(webSocket);
            client.StandardErrorReceived += (sender, e) => standardErrorMessages.Add(e);
            client.StandardOutputReceived += (sender, e) => standardOutputMessages.Add(e);
            client.ConnectionClosed += (sender, e) => closedEvent.Set();
            client.Start();

            closedEvent.WaitOne(TimeSpan.FromSeconds(1));

            Assert.Single(standardOutputMessages, "Hello standard output");
            Assert.Single(standardErrorMessages, "Hello standard error");

            Assert.False(client.Running);
        }
    }
}
