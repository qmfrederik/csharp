
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
        public const string PowerShell = "quamotion/powershell:0.103.70-bionic";
        public const string FFmpeg = "jrottenberg/ffmpeg:4.0-ubuntu";
        public const string Nginx = "nginx:1.13";
    }
}
