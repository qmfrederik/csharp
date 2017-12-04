using k8s;
using System;
using System.Threading.Tasks;

namespace exec
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var pod = args[0];
            var container = args.Length > 1 ? args[1] : null;

            var k8SClientConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile();
            var client = new Kubernetes(k8SClientConfig);

            var webSocket = await client.WebSocketNamespacedPodExecWithHttpMessagesAsync(pod, container: container, command: "/bin/bash").ConfigureAwait(false);

            ExecClient exec = new ExecClient(webSocket);
            exec.StandardErrorReceived += (sender, e) => Console.Error.Write(e);
            exec.StandardOutputReceived += (sender, e) => Console.Out.Write(e);
            exec.ConnectionClosed += (sender, e) => Environment.Exit(0);
            exec.Start();

            while (true)
            {
                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    await exec.Write("\n").ConfigureAwait(false);
                }
                else
                {
                    await exec.Write(key.KeyChar.ToString()).ConfigureAwait(false);
                }
            }
        }
    }
}
