using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KubernetesClient.IntegrationTests
{
    /// <summary>
    /// Provides methods for executing commands inside pods.
    /// </summary>
    public class PodExecutor : IPodExecutor
    {
        private readonly IKubernetes kubernetes;

        /// <summary>
        /// Initializes a new instance of the <see cref="PodExecutor"/> class.
        /// </summary>
        /// <param name="kubernetes">
        /// An connection to a Kubernetes cluster.
        /// </param>
        public PodExecutor(IKubernetes kubernetes)
        {
            this.kubernetes = kubernetes ?? throw new ArgumentNullException(nameof(kubernetes));
        }

        /// <inheritdoc/>
        public Task<int> RunPowerShellCommandInPod(V1Pod pod, string container, ILogger log, string command, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return this.ExecuteCommand(
                name: pod.Metadata.Name,
                @namespace: pod.Metadata.NamespaceProperty,
                container: container,
                command: new string[] { "/usr/bin/pwsh", "-NoLogo", "-NonInteractive", "-Command", command },
                tty: false,
                (console) =>
                {
                    if (log != null)
                    {
                        console.StandardError += (sender, e) => log?.LogError(e);
                        console.StandardOutput += (sender, e) => log?.LogInformation(e);
                    }

                    console.Run();
                },
                timeout,
                cancellationToken);
        }

        /// <inheritdoc/>
        public Task<int> RunBashCommandInPod(V1Pod pod, string container, ILogger log, string command, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (pod == null)
            {
                throw new ArgumentNullException(nameof(pod));
            }

            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return this.ExecuteCommand(
                name: pod.Metadata.Name,
                @namespace: pod.Metadata.NamespaceProperty,
                container: container,
                command: new string[] { "/bin/bash", "-c", command },
                tty: true,
                (console) =>
                {
                    if (log != null)
                    {
                        console.StandardError += (sender, e) => log?.LogError(e);
                        console.StandardOutput += (sender, e) => log?.LogInformation(e);
                    }

                    console.Run();
                },
                timeout,
                cancellationToken);
        }

        /// <inheritdoc/>
        public Task<int> RunConsoleCommandInPod(V1Pod pod, string container, ILogger log, string[] command, Action<ConsoleClient> action, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (pod == null)
            {
                throw new ArgumentNullException(nameof(pod));
            }

            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return this.ExecuteCommand(
                name: pod.Metadata.Name,
                @namespace: pod.Metadata.NamespaceProperty,
                container: container,
                command: command,
                tty: true,
                (console) =>
                {
                    if (log != null)
                    {
                        console.StandardError += (sender, e) => log.LogError(e);
                        console.StandardOutput += (sender, e) => log.LogInformation(e);
                    }

                    console.Run();

                    action(console);
                },
                timeout,
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task DownloadFileFromPod(V1Pod pod, string container, string path, Stream target, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (pod == null)
            {
                throw new ArgumentNullException(nameof(pod));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            var commandCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token).Token;

            using (WebSocket webSocket = await this.kubernetes.WebSocketNamespacedPodExecAsync(
                name: pod.Metadata.Name,
                @namespace: pod.Metadata.NamespaceProperty,
                command: new string[] { "/bin/cat", path },
                container: container,
                tty: false,
                cancellationToken: commandCancellationToken).ConfigureAwait(false))
            using (StreamDemuxer muxer = new StreamDemuxer(webSocket))
            using (Stream stream = muxer.GetStream(1, 0))
            using (Stream stdErrorStream = muxer.GetStream(2, null))
            using (StreamReader stdErrorReader = new StreamReader(stdErrorStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
            using (Stream errorStream = muxer.GetStream(3, null))
            using (StreamReader errorReader = new StreamReader(errorStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
            {
                muxer.Start();
                await stream.CopyToAsync(target).ConfigureAwait(false);

                var stdError = await stdErrorReader.ReadToEndAsync().ConfigureAwait(false);
                var error = await errorReader.ReadToEndAsync().ConfigureAwait(false);

                var status = SafeJsonConvert.DeserializeObject<V1Status>(error);
                var exitCode = KubernetesExtensions.GetExitCodeOrThrow(status);

                if (exitCode != 0)
                {
                    throw new FileNotFoundException($"The file {path} could not be downloaded from the pod {pod.Metadata.Name}. {stdError}", new KubernetesException(status));
                }
            }
        }

        /// <inheritdoc/>
        public async Task UploadFileToPod(V1Pod pod, string container, Stream source, string path, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (pod == null)
            {
                throw new ArgumentNullException(nameof(pod));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            var commandCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token).Token;

            using (WebSocket webSocket = await this.kubernetes.WebSocketNamespacedPodExecAsync(
                name: pod.Metadata.Name,
                @namespace: pod.Metadata.NamespaceProperty,
                command: new string[] { "/bin/bash", "-c", $"cat > {path}" },
                container: container,
                tty: false,
                cancellationToken: commandCancellationToken).ConfigureAwait(false))
            using (StreamDemuxer muxer = new StreamDemuxer(webSocket))
            using (Stream stream = muxer.GetStream(1, 0))
            using (Stream errorStream = muxer.GetStream(2, null))
            {
                muxer.Start();
                await source.CopyToAsync(stream).ConfigureAwait(false);
            }
        }

        public async Task<int> ExecuteCommand(string name, string @namespace, string container, IEnumerable<string> command, bool tty, Action<ConsoleClient> action, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (command == null)
            {
                throw new ArgumentNullException();
            }

            if (!command.Any())
            {
                throw new ArgumentOutOfRangeException(nameof(command));
            }

            var commandArray = command.ToArray();
            foreach (var c in commandArray)
            {
                if (c.Length > 0 && c[0] == 0xfeff)
                {
                    throw new InvalidOperationException($"Detected an attempt to execute a command which starts with a Unicode byte order mark (BOM). This is probably incorrect. The command was {c}");
                }
            }

            CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            var commandCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token).Token;

            try
            {
                using (WebSocket webSocket = await this.kubernetes.WebSocketNamespacedPodExecAsync(name: name, @namespace: @namespace, command: commandArray, container: container, tty: tty, cancellationToken: commandCancellationToken).ConfigureAwait(false))
                using (StreamDemuxer muxer = new StreamDemuxer(webSocket))
                using (Stream stream = muxer.GetStream(1, 0))
                using (Stream errorStream = muxer.GetStream(2, null))
                using (Stream systemErrorStream = muxer.GetStream(3, null))
                using (StreamReader systemErrorReader = new StreamReader(systemErrorStream))
                {
                    ConsoleClient console = new ConsoleClient(stream, stream, errorStream);

                    muxer.Start();

                    action(console);

                    await console.WaitForExit(commandCancellationToken).ConfigureAwait(false);

                    var errors = await systemErrorReader.ReadToEndAsync().ConfigureAwait(false);

                    // StatusError is defined here:
                    // https://github.com/kubernetes/kubernetes/blob/068e1642f63a1a8c48c16c18510e8854a4f4e7c5/staging/src/k8s.io/apimachinery/pkg/api/errors/errors.go#L37
                    var returnMessage = SafeJsonConvert.DeserializeObject<V1Status>(errors);
                    return GetExitCodeOrThrow(returnMessage);
                }
            }
            catch (HttpOperationException httpEx) when (httpEx.Body is V1Status)
            {
                throw new KubernetesException((V1Status)httpEx.Body);
            }
        }

        /// <summary>
        /// Determines the process' exit code based on a <see cref="V1Status"/> message.
        ///
        /// This will:
        /// - return 0 if the process completed successfully
        /// - return the exit code if the process completed with a non-zero exit code
        /// - throw a <see cref="KubernetesException"/> in all other cases.
        /// </summary>
        /// <param name="status">
        /// A <see cref="V1Status"/> object.
        /// </param>
        /// <returns>
        /// The process exit code.
        /// </returns>
        public static int GetExitCodeOrThrow(V1Status status)
        {
            if (status.Status == "Success")
            {
                return 0;
            }
            else if (status.Status == "Failure" && status.Reason == "NonZeroExitCode")
            {
                var exitCodeString = status.Details.Causes.FirstOrDefault(c => c.Reason == "ExitCode")?.Message;

                if (int.TryParse(exitCodeString, out int exitCode))
                {
                    return exitCode;
                }
                else
                {
                    throw new KubernetesException(status);
                }
            }
            else
            {
                throw new KubernetesException(status);
            }
        }

    }
}
