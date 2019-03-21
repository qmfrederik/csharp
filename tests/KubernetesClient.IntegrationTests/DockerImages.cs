
using System;
using System.Collections.Generic;
using System.Text;

namespace KubernetesClient.IntegrationTests
{
    /// <summary>
    /// Contains the names of commonly used Docker images.
    /// </summary>
    internal static class DockerImages
    {
        public const string PowerShell = "mcr.microsoft.com/powershell:6.1.3-ubuntu-bionic";
        public const string FFmpeg = "jrottenberg/ffmpeg:4.0-ubuntu";
        public const string Nginx = "nginx:1.13";
    }
}
