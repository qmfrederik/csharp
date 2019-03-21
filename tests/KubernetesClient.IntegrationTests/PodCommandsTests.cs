using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KubernetesClient.IntegrationTests
{
    /// <summary>
    /// Tests the <see cref="PodExecutor"/> class.
    /// </summary>
    public class PodExecutorTests
    {
        private readonly IKubernetes kubernetes;
        private readonly StringBuilder standardOutput = new StringBuilder();
        private readonly StringBuilder errorOutput = new StringBuilder();

        /// <summary>
        /// Initializes a new instance of the <see cref="PodExecutorTests"/> class.
        /// </summary>
        public PodExecutorTests()
        {
            var kubernetesConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath: "minikube.config");
            this.kubernetes = new Kubernetes(kubernetesConfig);
        }

        /// <summary>
        /// Tests the <see cref="PodExecutor.RunPowerShellCommandInPod(V1Pod, string, ILogger, string, TimeSpan, CancellationToken)"/>
        /// method, in a scenario where the wrong pod is specified.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous test.
        /// </returns>
        [Fact(Timeout = 60000)]
        public async Task RunPowerShellCommandInPodInWrongContainerTest()
        {
            var command = new Func<PodExecutor, V1Pod, ILogger, Task<int>>(
                (podExecutor, pod, logger) =>
                {
                    return podExecutor.RunPowerShellCommandInPod(pod, "runner", logger, "Write-Host \"Hello, World!\"", TimeSpan.FromMinutes(5), CancellationToken.None);
                });

            var ex = await Assert.ThrowsAsync<KubernetesException>(() => this.Run(command)).ConfigureAwait(false);
            Assert.Equal("container runner is not valid for pod podexecutortests-runpowershellcommandinpodinwrongcontainertest-1", ex.Message);
        }

        /// <summary>
        /// Tests the <see cref="PodExecutor.RunPowerShellCommandInPod(V1Pod, string, ILogger, string, TimeSpan, CancellationToken)"/>
        /// method.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous test.
        /// </returns>
        [Fact(Timeout = 60000)]
        public async Task RunPowerShellCommandInPodTest()
        {
            var command = new Func<PodExecutor, V1Pod, ILogger, Task<int>>(
                (podExecutor, pod, logger) =>
                {
                    return podExecutor.RunPowerShellCommandInPod(pod, "powershell", logger, "Write-Host \"Hello, World!\"", TimeSpan.FromMinutes(5), CancellationToken.None);
                });

            var exitCode = await this.Run(command).ConfigureAwait(false);
            Assert.Equal(0, exitCode);
            Assert.Equal(
                @"Hello, World!

",
                this.standardOutput.ToString(),
                ignoreLineEndingDifferences: true);
            Assert.Equal(string.Empty, this.errorOutput.ToString());
        }

        /// <summary>
        /// Tests the <see cref="PodExecutor.RunPowerShellCommandInPod(V1Pod, string, ILogger, string, TimeSpan, CancellationToken)"/>
        /// method, in a scenario where a non-zero exit code is passed.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous test.
        /// </returns>
        [Fact(Timeout = 60000)]
        public async Task RunPowerShellCommandWithExitCodeInPodTest()
        {
            var command = new Func<PodExecutor, V1Pod, ILogger, Task<int>>(
                (podExecutor, pod, logger) =>
                {
                    return podExecutor.RunPowerShellCommandInPod(pod, "powershell", logger, "Exit 5", TimeSpan.FromMinutes(5), CancellationToken.None);
                });

            var exitCode = await this.Run(command).ConfigureAwait(false);
            Assert.Equal(5, exitCode);
            Assert.Equal(string.Empty, this.standardOutput.ToString(), ignoreLineEndingDifferences: true);
            Assert.Equal(string.Empty, this.errorOutput.ToString());
        }

        /// <summary>
        /// Test the <see cref="PodExecutor.RunBashCommandInPod(V1Pod, string, ILogger, string, TimeSpan, CancellationToken)"/>
        /// method.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous test.
        /// </returns>
        [Fact(Timeout = 60000)]
        public async Task RunBashCommandInPodTest()
        {
            var command = new Func<PodExecutor, V1Pod, ILogger, Task<int>>(
                (podExecutor, pod, logger) =>
                {
                    return podExecutor.RunBashCommandInPod(pod, "powershell", logger, "echo 'Hello, World!'", TimeSpan.FromMinutes(5), CancellationToken.None);
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
            var command = new Func<PodExecutor, V1Pod, ILogger, Task<int>>(
                (podExecutor, pod, logger) =>
                {
                    return podExecutor.RunBashCommandInPod(pod, "powershell", logger, "exit 5", TimeSpan.FromMinutes(5), CancellationToken.None);
                });

            var exitCode = await this.Run(command).ConfigureAwait(false);
            Assert.Equal(5, exitCode);
            Assert.Equal(string.Empty, this.standardOutput.ToString(), ignoreLineEndingDifferences: true);
            Assert.Equal(string.Empty, this.errorOutput.ToString());
        }

        /// <summary>
        /// Tests the <see cref="PodExecutor.UploadFileToPod(V1Pod, string, Stream, string, TimeSpan, CancellationToken)"/>
        /// method.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous test.
        /// </returns>
        [Fact(Timeout = 60000)]
        public async Task UploadFileToPodTest()
        {
            // Use a 5MB large file.
            byte[] data = new byte[5 * 1024 * 1024];
            Random random = new Random();
            random.NextBytes(data);

            var sha = new SHA256Managed();
            byte[] checksum = sha.ComputeHash(data);
            var hash = BitConverter.ToString(checksum).Replace("-", string.Empty).ToLower();

            using (MemoryStream stream = new MemoryStream(data))
            {
                var command = new Func<PodExecutor, V1Pod, ILogger, Task<int>>(
                    async (podExecutor, pod, logger) =>
                    {
                        await podExecutor.UploadFileToPod(pod, "powershell", stream, "/file", TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                        await podExecutor.RunBashCommandInPod(pod, "powershell", logger, "sha256sum /file", TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                        return 0;
                    });

                var exitCode = await this.Run(command).ConfigureAwait(false);
                Assert.Equal($"{hash}  /file\r\n\n", this.standardOutput.ToString(), ignoreLineEndingDifferences: true);
                Assert.Equal(string.Empty, this.errorOutput.ToString());
            }
        }

        /// <summary>
        /// Tests the <see cref="PodExecutor.DownloadFileFromPod(V1Pod, string, string, Stream, TimeSpan, CancellationToken)"/>
        /// method using a file filled with the character 'a'
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous test.
        /// </returns>
        [Fact(Timeout = 60000)]
        public async Task DownloadStaticFileFromPodTest()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                var command = new Func<PodExecutor, V1Pod, ILogger, Task<int>>(
                    async (podExecutor, pod, logger) =>
                    {
                        await podExecutor.RunBashCommandInPod(pod, "powershell", null, "head -c 10485760 < /dev/zero | tr '\\0' '\\141' > /file", TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                        await podExecutor.RunBashCommandInPod(pod, "powershell", logger, "sha256sum /file", TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                        await podExecutor.DownloadFileFromPod(pod, "powershell", "/file", stream, TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                        return 0;
                    });

                var sha = new SHA256Managed();

                var exitCode = await this.Run(command).ConfigureAwait(false);

                stream.Position = 0;
                byte[] checksum = sha.ComputeHash(stream);
                var hash = BitConverter.ToString(checksum).Replace("-", string.Empty).ToLower();

                Assert.Equal($"{hash}  /file\r\n\n", this.standardOutput.ToString(), ignoreLineEndingDifferences: true);
                Assert.Equal(string.Empty, this.errorOutput.ToString());
            }
        }

        /// <summary>
        /// Tests the <see cref="PodExecutor.DownloadFileFromPod(V1Pod, string, string, Stream, TimeSpan, CancellationToken)"/>
        /// method using a file filled with random bytes.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous test.
        /// </returns>
        [Fact(Timeout = 60000)]
        public async Task DownloadRandomFileFromPodTest()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                var command = new Func<PodExecutor, V1Pod, ILogger, Task<int>>(
                    async (podExecutor, pod, logger) =>
                    {
                        await podExecutor.RunBashCommandInPod(pod, "powershell", null, "dd if=/dev/urandom of=/file bs=1M count=10", TimeSpan.FromMinutes(1), CancellationToken.None).ConfigureAwait(false);
                        await podExecutor.RunBashCommandInPod(pod, "powershell", logger, "sha256sum /file", TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                        await podExecutor.DownloadFileFromPod(pod, "powershell", "/file", stream, TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                        return 0;
                    });

                var sha = new SHA256Managed();

                var exitCode = await this.Run(command).ConfigureAwait(false);

                stream.Position = 0;
                byte[] checksum = sha.ComputeHash(stream);
                var hash = BitConverter.ToString(checksum).Replace("-", string.Empty).ToLower();
                Assert.Equal($"{hash}  /file\r\n\n", this.standardOutput.ToString(), ignoreLineEndingDifferences: true);
                Assert.Equal(string.Empty, this.errorOutput.ToString());
            }
        }

        /// <summary>
        /// Tests the <see cref="PodExecutor.DownloadFileFromPod(V1Pod, string, string, Stream, TimeSpan, CancellationToken)"/>
        /// method using a file which does not exist.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous test.
        /// </returns>
        [Fact(Timeout = 60000)]
        public async Task DownloadMissingFileFromPodTest()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                var command = new Func<PodExecutor, V1Pod, ILogger, Task<int>>(
                    async (podExecutor, pod, logger) =>
                    {
                        await podExecutor.DownloadFileFromPod(pod, "powershell", "/file-which-does-not-exist", stream, TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                        return 0;
                    });

                await Assert.ThrowsAsync<FileNotFoundException>(() => this.Run(command)).ConfigureAwait(false);

                Assert.Equal(0, stream.Length);
            }
        }

        /// <summary>
        /// Tests running a command in a pod.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous test.
        /// </returns>
        [Fact(Timeout = 60000)]
        public async Task RunCommandInPodTest()
        {
            var command = new Func<PodExecutor, V1Pod, ILogger, Task<int>>(
                async (podExecutor, pod, logger) =>
                {
                    var exit = await podExecutor.RunConsoleCommandInPod(
                        pod,
                        "ffmpeg",
                        logger,
                        new string[]
                        {
                            "ffmpeg",
                            "-f",
                            "lavfi",
                            "-i",
                            "testsrc=duration=600:size=1280x720:rate=30",
                            "/video.mpg",

                            // "-nostdin",
                            "-y"
                        },
                        (console) =>
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(3));
                            console.Kill();
                        },
                        TimeSpan.FromMinutes(1),
                        CancellationToken.None).ConfigureAwait(false);

                    using (Stream stream = File.Open("video.mpg", FileMode.Create, FileAccess.ReadWrite))
                    {
                        await podExecutor.DownloadFileFromPod(pod, "ffmpeg", "/video.mpg", stream, TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                    }

                    return exit;
                });

            var exitCode = await this.Run(command, DockerImages.FFmpeg, "ffmpeg").ConfigureAwait(false);
            Assert.Equal(255, exitCode);
            Assert.NotEqual(string.Empty, this.standardOutput.ToString());
            Assert.Equal(string.Empty, this.errorOutput.ToString());
        }

        private async Task<int> Run(Func<PodExecutor, V1Pod, ILogger, Task<int>> command, string image = DockerImages.PowerShell, string containerName = "powershell", [CallerMemberName] string caller = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            int exitCode = 0;

            PodManager podManager = new PodManager(this.kubernetes, Mock.Of<ILogger>());
            var pod = await podManager.Create(
                1,
                1,
                "default",
                new V1Pod()
                {
                    ApiVersion = "v1",
                    Kind = "Pod",
                    Metadata = new V1ObjectMeta()
                    {
                        Name = $"{nameof(PodExecutorTests).ToLowerInvariant()}-{caller.ToLowerInvariant()}"
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
                PodExecutor executor = new PodExecutor(this.kubernetes);

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

                exitCode = await command(executor, pod, logger.Object).ConfigureAwait(false);
            }
            finally
            {
                await podManager.Delete(pod, CancellationToken.None).ConfigureAwait(false);
            }

            return exitCode;
        }
    }
}
