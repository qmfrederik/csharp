using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KubernetesClient.IntegrationTests
{
    /// <summary>
    /// Provides methods for executing commands inside pods.
    /// </summary>
    public interface IPodExecutor
    {
        /// <summary>
        /// Executes a PowerShell command in a pod.
        /// </summary>
        /// <param name="pod">
        /// The pod which contains the container in which to execute the command.
        /// </param>
        /// <param name="container">
        /// The container in which to run the command.
        /// </param>
        /// <param name="log">
        /// A logger which is used for diagnostic logging.
        /// </param>
        /// <param name="command">
        /// The PowerShell command to execute.
        /// </param>
        /// <param name="timeout">
        /// The amount of time the command is allowed to run.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> which can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous operation.
        /// </returns>
        Task<int> RunPowerShellCommandInPod(V1Pod pod, string container, ILogger log, string command, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Runs a Bash command in a pod.
        /// </summary>
        /// <param name="pod">
        /// The pod which contains the container in which to execute the ocmmand.
        /// </param>
        /// <param name="container">
        /// The container in which to run the command.
        /// </param>
        /// <param name="log">
        /// A logger which is used for diagnostic logging.
        /// </param>
        /// <param name="command">
        /// The command to execute.
        /// </param>
        /// <param name="timeout">
        /// The amount of time the command is allowed to run.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> which can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous operation.
        /// </returns>
        Task<int> RunBashCommandInPod(V1Pod pod, string container, ILogger log, string command, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Downloads a file from a pod.
        /// </summary>
        /// <param name="pod">
        /// The pod from which to download the file.
        /// </param>
        /// <param name="container">
        /// The container which contains the file.
        /// </param>
        /// <param name="path">
        /// The path to the file.
        /// </param>
        /// <param name="target">
        /// A <see cref="Stream"/> which will receive the file contents.
        /// </param>
        /// <param name="timeout">
        /// The amount of time the operation can run.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> which can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous operation.
        /// </returns>
        Task DownloadFileFromPod(V1Pod pod, string container, string path, Stream target, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Runs a console command in a pod.
        /// </summary>
        /// <param name="pod">
        /// The pod within which to run the command.
        /// </param>
        /// <param name="container">
        /// The container within which to run the command.
        /// </param>
        /// <param name="log">
        /// The logger to which to write the output.
        /// </param>
        /// <param name="command">
        /// The command to execute.
        /// </param>
        /// <param name="action">
        /// A console client, which monitors the command progress.
        /// </param>
        /// <param name="timeout">
        /// The amount of time the operation can run.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> which can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous operation.
        /// </returns>
        Task<int> RunConsoleCommandInPod(V1Pod pod, string container, ILogger log, string[] command, Action<ConsoleClient> action, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Uploads a file to a pod.
        /// </summary>
        /// <param name="pod">
        /// The pod to which to upload the file.
        /// </param>
        /// <param name="container">
        /// The container to which to upload the file.
        /// </param>
        /// <param name="source">
        /// A <see cref="Stream"/> which contains the file to upload.
        /// </param>
        /// <param name="path">
        /// The path to upload the file to.
        /// </param>
        /// <param name="timeout">
        /// The amount of time the operation can take.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> which can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous operation.
        /// </returns>
        Task UploadFileToPod(V1Pod pod, string container, Stream source, string path, TimeSpan timeout, CancellationToken cancellationToken);
    }
}