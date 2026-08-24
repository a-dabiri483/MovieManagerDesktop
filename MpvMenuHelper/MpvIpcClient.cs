using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;

namespace MpvMenuHelper
{
    public class MpvIpcClient
    {
        private readonly string _pipeName;

        public MpvIpcClient(string pipeName)
        {
            string clean = pipeName.Trim().Trim('\"', '\'');
            if (clean.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(9);
            else if (clean.StartsWith(@"\\\\.\\pipe\\", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(13);
            else if (clean.Contains("pipe\\"))
                clean = clean.Substring(clean.IndexOf("pipe\\") + 5);
            else if (clean.Contains("pipe/"))
                clean = clean.Substring(clean.IndexOf("pipe/") + 5);

            _pipeName = clean.TrimStart('\\', '/');
        }

        public async Task SendCommandAsync(params object[] commandArgs)
        {
            try
            {
                using var pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                await pipeClient.ConnectAsync(1200);
                using var writer = new StreamWriter(pipeClient) { AutoFlush = true };

                var commandObj = new { command = commandArgs };
                string json = JsonSerializer.Serialize(commandObj);
                await writer.WriteLineAsync(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IPC Error on pipe '{_pipeName}': {ex.Message}");
            }
        }

        public void SendCommand(params object[] commandArgs)
        {
            Task.Run(() => SendCommandAsync(commandArgs));
        }
    }
}
