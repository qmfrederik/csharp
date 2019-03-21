using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KubernetesClient.IntegrationTests
{
    class PodManager
    {
        private readonly IKubernetes kubernetes;
        private readonly ILogger logger;
        private readonly TimeSpan timeout = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Initializes a new instance of the <see cref="PodManager"/> class.
        /// </summary>
        /// <param name="kubernetes">
        /// The Kubernetes API.
        /// </param>
        /// <param name="logger">
        /// A logger which is used when logging diagnostic messages.
        /// </param>
        public PodManager(IKubernetes kubernetes, ILogger logger)
        {
            this.kubernetes = kubernetes ?? throw new ArgumentNullException(nameof(kubernetes));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<V1Pod> Create(string @namespace, V1Pod template, Dictionary<string, string> env, CancellationToken cancellationToken)
        {
            if (template.Metadata.Labels == null)
            {
                template.Metadata.Labels = new Dictionary<string, string>();
            }

            if (template.Spec.Containers[0].Env == null)
            {
                template.Spec.Containers[0].Env = new List<V1EnvVar>();
            }

            foreach (var var in env)
            {
                template.Spec.Containers[0].Env.Add(new V1EnvVar() { Name = var.Key, Value = var.Value });
            }

            V1Pod pod = null;

            try
            {
                pod = await this.kubernetes.CreateNamespacedPodAsync(template, @namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (HttpOperationException ex)
            {
                this.logger.LogError($"Failed to create a pod. The podspec was '{YamlConvert.Serialize(template)}', the server response was '{ex.Response.Content}' with a status code of {ex.Response.StatusCode}");
                throw;
            }

            AsyncAutoResetEvent mre = new AsyncAutoResetEvent();
            Exception error = null;
            bool deleting = false;

            using (var watcher = await this.kubernetes.WatchNamespacedPodAsync(
                pod.Metadata.Name,
                @namespace,
                resourceVersion: pod.Metadata.ResourceVersion,
                onEvent: (type, updatedPod) =>
                {
                    pod = updatedPod;

                    int initContainersReady = 0;
                    int totalInitContainers = 0;

                    if (pod.Status?.InitContainerStatuses != null)
                    {
                        totalInitContainers = pod.Status.InitContainerStatuses.Count();
                        initContainersReady = pod.Status.InitContainerStatuses.Count(c => c.Ready);
                    }

                    int containersReady = 0;
                    int totalContainers = 0;

                    if (pod.Status?.ContainerStatuses != null)
                    {
                        totalContainers = pod.Status.ContainerStatuses.Count();
                        containersReady = pod.Status.ContainerStatuses.Count(c => c.Ready);
                    }

                    this.logger.LogInformation($"Pod {pod.Metadata.Name} status has changed: {type}. The pod status is {pod.Status.Message}, phase {pod.Status.Phase} and IP {pod.Status.PodIP}.");
                    this.logger.LogInformation($"Pod {pod.Metadata.Name} Init containers ready: {initContainersReady}/{totalInitContainers}; containers ready: {containersReady}/{totalContainers}");

                    if (type == WatchEventType.Deleted)
                    {
                        mre.Set();
                    }

                    // Wait for the pod to be ready. Since we work with init containers, merely waiting for the pod to have an IP address is not enough -
                    // we need both 'Running' status _and_ and IP address
                    if (pod != null && pod.Status.Phase == "Running" && pod.Status.PodIP != null)
                    {
                        mre.Set();
                    }

                    // However, if the pod fails to start because of image pull errors, bail out - this is not something we'll
                    // be able to fix here.
                    if (!deleting && pod != null && pod.Status.InitContainerStatuses != null && pod.Status.InitContainerStatuses.Any(c => c.State?.Waiting?.Reason == "ErrImagePull"))
                    {
                        var failingInitContainers = pod.Status.InitContainerStatuses.Where(c => c.State.Waiting != null && c.State.Waiting.Reason == "ErrImagePull").ToArray();

                        foreach (var failingInitContainer in failingInitContainers)
                        {
                            this.logger.LogError($"Failed to create the pod {pod.Metadata.Name}. The image for init container {failingInitContainer.Name} could not be pulled: {error}");
                        }

                        error = new KubernetesException(failingInitContainers.First().State.Waiting.Message);

                        // Trigger the deletion of this pod. Keep using the watcher to detect the moment at which the pod has been deleted
                        var task = this.kubernetes.DeleteNamespacedPodAsync(pod.Metadata.Name, pod.Metadata.NamespaceProperty, cancellationToken: cancellationToken);
                        deleting = true;
                    }
                },
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                await mre.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (error != null)
            {
                throw error;
            }

            return pod;
        }

        /// <summary>
        /// Deletes a pod.
        /// </summary>
        /// <param name="pod">
        /// The pod to delete.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> which can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous operation.
        /// </returns>
        public async Task Delete(V1Pod pod, CancellationToken cancellationToken)
        {
            if (pod == null)
            {
                throw new ArgumentNullException(nameof(pod));
            }

            AsyncAutoResetEvent mre = new AsyncAutoResetEvent();

            using (var watcher = await this.kubernetes.WatchNamespacedPodAsync(
                pod.Metadata.Name,
                pod.Metadata.NamespaceProperty,
                onEvent: (eventType, p) =>
                {
                    if (eventType == WatchEventType.Deleted)
                    {
                        mre.Set();
                    }
                },
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                await this.kubernetes.DeleteNamespacedPodAsync(pod.Metadata.Name, pod.Metadata.NamespaceProperty, cancellationToken: cancellationToken).ConfigureAwait(false);

                await Task.WhenAny(mre.WaitAsync(cancellationToken), Task.Delay(this.timeout)).ConfigureAwait(false);
            }

            if (!mre.IsSet)
            {
                throw new InvalidOperationException($"Could not delete the pod {pod.Metadata.NamespaceProperty}/{pod.Metadata.Name} within {this.timeout.TotalSeconds}s.");
            }
        }
    }
}
