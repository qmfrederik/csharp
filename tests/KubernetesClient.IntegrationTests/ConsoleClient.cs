using Nito.AsyncEx;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KubernetesClient.IntegrationTests
{
    /// <summary>
    /// A client for interacting with a remote console with standard intput, output and error streams.
    /// </summary>
    public class ConsoleClient
    {
        private readonly Stream stdIn;
        private readonly Stream stdErr;
        private readonly Stream stdOut;
        private readonly AsyncManualResetEvent stdOutClosed = new AsyncManualResetEvent(false);
        private readonly AsyncManualResetEvent stdErrClosed = new AsyncManualResetEvent(false);

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleClient"/> class.
        /// </summary>
        /// <param name="stdOut">
        /// A standard output stream.
        /// </param>
        /// <param name="stdIn">
        /// A standard input stream.
        /// </param>
        /// <param name="stdErr">
        /// A standard error stream.
        /// </param>
        public ConsoleClient(Stream stdOut, Stream stdIn, Stream stdErr)
        {
            this.stdErr = stdErr ?? throw new ArgumentNullException(nameof(stdErr));
            this.stdIn = stdIn ?? throw new ArgumentNullException(nameof(stdIn));
            this.stdOut = stdOut ?? throw new ArgumentNullException(nameof(stdOut));
        }

        /// <summary>
        /// An event which is raised when standard output is received.
        /// </summary>
        public event EventHandler<ArraySegment<byte>> StandardBinaryOutput;

        /// <summary>
        /// An event which is raised when standard output is received.
        /// </summary>
        public event EventHandler<string> StandardOutput;

        /// <summary>
        /// An event which is raised when standard error data is received.
        /// </summary>
        public event EventHandler<ArraySegment<byte>> StandardBinaryError;

        /// <summary>
        /// An event which is raised when standard error data is received.
        /// </summary>
        public event EventHandler<string> StandardError;

        /// <summary>
        /// Starts processing the standard input and output streams.
        /// </summary>
        public void Run()
        {
            var standardOutputTask = this.RunLoop(this.stdOut, this.StandardOutput, this.StandardBinaryOutput, this.stdOutClosed);
            var standardErrorTask = this.RunLoop(this.stdErr, this.StandardError, this.StandardBinaryError, this.stdErrClosed);
        }

        /// <summary>
        /// Kills the remote process.
        /// </summary>
        public void Kill()
        {
            // 0x03 = CTRL+C , End Of Text in ASCII
            this.stdIn.WriteByte(0x03);
        }

        /// <summary>
        /// Waits for the program to exit.
        /// </summary>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> which can be used to cancel the aynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous operation.
        /// </returns>
        public async Task WaitForExit(CancellationToken cancellationToken)
        {
            await this.stdOutClosed.WaitAsync(cancellationToken).ConfigureAwait(false);
            await this.stdErrClosed.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes a line to the remote console.
        /// </summary>
        /// <param name="line">
        /// The line to write.
        /// </param>
        public void WriteLine(string line)
        {
            byte[] data = Encoding.UTF8.GetBytes(line);
            this.stdIn.Write(data, 0, data.Length);
        }

        /// <summary>
        /// The main run loop for a stream (standard output/error).
        /// </summary>
        /// <param name="stream">
        /// The stream to analyze.
        /// </param>
        /// <param name="handler">
        /// A handler which handles incoming data.
        /// </param>
        /// <param name="binaryHandler">
        /// A handler which handles incoming data in binary form.
        /// </param>
        /// <param name="onClosed">
        /// A event on which to signal the closure of the stream.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous operation.
        /// </returns>
        protected async Task RunLoop(Stream stream, EventHandler<string> handler, EventHandler<ArraySegment<byte>> binaryHandler, AsyncManualResetEvent onClosed)
        {
            byte[] buffer = new byte[0x100];
            var currentLine = string.Empty;

            while (true)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                if (handler != null)
                {
                    var text = Encoding.UTF8.GetString(buffer, 0, read);
                    var lines = text.Split('\n');

                    if (lines.Length == 1)
                    {
                        if (text.EndsWith('\n'))
                        {
                            handler?.Invoke(this, currentLine + text);
                        }
                        else
                        {
                            currentLine += lines[0];
                        }
                    }
                    else
                    {
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (i == 0)
                            {
                                handler?.Invoke(this, currentLine + lines[i] + '\n');
                            }
                            else if (i < lines.Length - 1)
                            {
                                handler?.Invoke(this, lines[i] + '\n');
                            }
                            else
                            {
                                if (text.EndsWith('\n'))
                                {
                                    handler?.Invoke(this, lines[i] + '\n');
                                    currentLine = string.Empty;
                                }
                                else
                                {
                                    currentLine = lines[i];
                                }
                            }
                        }
                    }
                }

                if (binaryHandler != null)
                {
                    ArraySegment<byte> segment = new ArraySegment<byte>(buffer, 0, read);
                    binaryHandler?.Invoke(this, segment);
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                handler?.Invoke(this, currentLine + '\n');
            }

            onClosed.Set();
        }
    }
}
