using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KubernetesClient.IntegrationTests
{
    public class KubernetesExecTests
    {
        private readonly IKubernetes kubernetes;
        private readonly StringBuilder standardOutput = new StringBuilder();
        private readonly StringBuilder errorOutput = new StringBuilder();

        /// <summary>
        /// Initializes a new instance of the <see cref="PodExecutorTests"/> class.
        /// </summary>
        public KubernetesExecTests()
        {
            var kubernetesConfig = KubernetesClientConfiguration.BuildDefaultConfig();
            this.kubernetes = new Kubernetes(kubernetesConfig);
        }

        /// <summary>
        /// Test the <see cref="Kubernetes.RunBashCommandInPod(V1Pod, string, ILogger, string, TimeSpan, CancellationToken)"/>
        /// method.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous test.
        /// </returns>
        [Fact(Timeout = 60000)]
        public async Task RunBashCommandInPodTest()
        {
            var command = new Func<IKubernetes, V1Pod, ILogger, Task<int>>(
                (kubernetes, pod, logger) =>
                {
                    return kubernetes.RunBashCommandInPod(pod, "powershell", logger, "echo 'Hello, World!'", TimeSpan.FromMinutes(5), CancellationToken.None);
                });

            var exitCode = await this.Run(command).ConfigureAwait(false);
            Assert.Equal(0, exitCode);
            Assert.Equal("Hello, World!\r\n\n", this.standardOutput.ToString(), ignoreLineEndingDifferences: true);
            Assert.Equal(string.Empty, this.errorOutput.ToString());
        }

        /// <summary>
        /// Test the <see cref="PodExecutor.RunBashCommandInPod(V1Pod, string, ILogger, string, TimeSpan, CancellationToken)"/>
        /// method, in a scenario where a non-zero exit code is returned.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous test.
        /// </returns>
        [Fact(Timeout = 60000)]
        public async Task RunBashWithExitCodeInPodTest()
        {
            var command = new Func<IKubernetes, V1Pod, ILogger, Task<int>>(
                (kubernetes, pod, logger) =>
                {
                    return kubernetes.RunBashCommandInPod(pod, "powershell", logger, "exit 5", TimeSpan.FromMinutes(5), CancellationToken.None);
                });

            var exitCode = await this.Run(command).ConfigureAwait(false);
            Assert.Equal(5, exitCode);
            Assert.Equal(string.Empty, this.standardOutput.ToString(), ignoreLineEndingDifferences: true);
            Assert.Equal(string.Empty, this.errorOutput.ToString());
        }

        private async Task<int> Run(Func<IKubernetes, V1Pod, ILogger, Task<int>> command, string image = DockerImages.PowerShell, string containerName = "powershell", [CallerMemberName] string caller = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            int exitCode = 0;

            PodManager podManager = new PodManager(this.kubernetes, Mock.Of<ILogger>());
            var pod = await podManager.Create(
                "default",
                new V1Pod()
                {
                    ApiVersion = "v1",
                    Kind = "Pod",
                    Metadata = new V1ObjectMeta()
                    {
                        Name = $"{nameof(KubernetesExecTests).ToLowerInvariant()}-{caller.ToLowerInvariant()}"
                    },
                    Spec = new V1PodSpec()
                    {
                        Containers = new List<V1Container>()
                         {
                             new V1Container()
                             {
                                  Image = image,
                                  Name = containerName,
                                  Command = new List<string>()
                                  {
                                      "/bin/bash",
                                      "-c",
                                      "--"
                                  },
                                  Args = new List<string>()
                                  {
                                      "trap : TERM INT; sleep infinity & wait"
                                  }
                             }
                         }
                    }
                },
                new Dictionary<string, string>(),
                CancellationToken.None).ConfigureAwait(false);

            try
            {
                var logger = new Mock<ILogger>(MockBehavior.Strict);
                logger.Setup(l => l.Log<object>(LogLevel.Error, 0, It.IsAny<object>(), null, It.IsAny<Func<object, Exception, string>>()))
                    .Callback<LogLevel, EventId, object, Exception, Func<object, Exception, string>>(
                    (logLevel, eventId, message, exception, formatter) =>
                    {
                        this.errorOutput.Append(message);
                    });
                logger.Setup(l => l.Log<object>(LogLevel.Information, 0, It.IsAny<object>(), null, It.IsAny<Func<object, Exception, string>>()))
                    .Callback<LogLevel, EventId, object, Exception, Func<object, Exception, string>>(
                    (logLevel, eventId, message, exception, formatter) =>
                    {
                        this.standardOutput.Append(message);
                    });

                exitCode = await command(this.kubernetes, pod, logger.Object).ConfigureAwait(false);
            }
            finally
            {
                await podManager.Delete(pod, CancellationToken.None).ConfigureAwait(false);
            }

            return exitCode;
        }
    }
}
