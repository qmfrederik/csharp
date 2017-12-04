using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace k8s
{
    public partial class Kubernetes
    {
        /// <summary>
        /// Gets a function which returns a <see cref="WebSocketBuilder"/> which <see cref="Kubernetes"/> will use to
        /// create a new <see cref="WebSocket"/> connection to the Kubernetes cluster.
        /// </summary>
        public Func<WebSocketBuilder> CreateWebSocketBuilder { get; set; } = () => new WebSocketBuilder();

        /// <inheritdoc/>
        public async Task<WebSocket> WebSocketNamespacedPodExecWithHttpMessagesAsync(string name, string @namespace = "default", string command = "/bin/sh", string container = null, bool stderr = true, bool stdin = true, bool stdout = true, bool tty = true, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (@namespace == null)
            {
                throw new ArgumentNullException(nameof(@namespace));
            }

            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            // Tracing
            bool _shouldTrace = ServiceClientTracing.IsEnabled;
            string _invocationId = null;
            if (_shouldTrace)
            {
                _invocationId = ServiceClientTracing.NextInvocationId.ToString();
                Dictionary<string, object> tracingParameters = new Dictionary<string, object>();
                tracingParameters.Add("command", command);
                tracingParameters.Add("container", container);
                tracingParameters.Add("name", name);
                tracingParameters.Add("namespace", @namespace);
                tracingParameters.Add("stderr", stderr);
                tracingParameters.Add("stdin", stdin);
                tracingParameters.Add("stdout", stdout);
                tracingParameters.Add("tty", tty);
                tracingParameters.Add("cancellationToken", cancellationToken);
                ServiceClientTracing.Enter(_invocationId, this, nameof(WebSocketNamespacedPodExecWithHttpMessagesAsync), tracingParameters);
            }

            // Construct URL
            var uriBuilder = new UriBuilder(BaseUri);
            uriBuilder.Scheme = BaseUri.Scheme == "https" ? "wss" : "ws";

            if (!uriBuilder.Path.EndsWith("/"))
            {
                uriBuilder.Path += "/";
            }

            uriBuilder.Path += $"api/v1/namespaces/{@namespace}/pods/{name}/exec";

            List<string> _queryParameters = new List<string>();
            _queryParameters.Add(string.Format("command={0}", Uri.EscapeDataString(command)));

            if (container != null)
            {
                _queryParameters.Add(string.Format("container={0}", Uri.EscapeDataString(container)));
            }

            _queryParameters.Add(string.Format("stderr={0}", stderr ? 1 : 0));
            _queryParameters.Add(string.Format("stdin={0}", stdin ? 1 : 0));
            _queryParameters.Add(string.Format("stdout={0}", stdout ? 1 : 0));
            _queryParameters.Add(string.Format("tty={0}", tty ? 1 : 0));

            uriBuilder.Query = string.Join("&", _queryParameters);

            // Create WebSocket transport objects
            WebSocketBuilder webSocketBuilder = this.CreateWebSocketBuilder();

            // Set Headers
            if (customHeaders != null)
            {
                foreach (var _header in customHeaders)
                {
                    webSocketBuilder.SetRequestHeader(_header.Key, string.Join(" ", _header.Value));
                }
            }

            // Set Credentials
            foreach (var cert in this.HttpClientHandler.ClientCertificates)
            {
                webSocketBuilder.AddClientCertificate(cert);
            }

            HttpRequestMessage message = new HttpRequestMessage();
            await this.Credentials.ProcessHttpRequestAsync(message, cancellationToken);

            foreach (var _header in message.Headers)
            {
                webSocketBuilder.SetRequestHeader(_header.Key, string.Join(" ", _header.Value));
            }

            // Send Request
            cancellationToken.ThrowIfCancellationRequested();

            WebSocket webSocket = null;

            try
            {
                webSocket = await webSocketBuilder.BuildAndConnectAsync(uriBuilder.Uri, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ServiceClientTracing.Error(_invocationId, ex);
                throw;
            }
            finally
            {
                if (_shouldTrace)
                {
                    ServiceClientTracing.Exit(_invocationId, null);
                }
            }

            return webSocket;
        }
    }
}
