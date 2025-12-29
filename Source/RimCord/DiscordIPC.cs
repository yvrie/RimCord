using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace RimCord
{
    internal class DiscordButton
    {
        public string label { get; set; }
        public string url { get; set; }
    }

    internal class DiscordTimestamps
    {
        public long? start { get; set; }
        public long? end { get; set; }
    }

    internal class DiscordAssets
    {
        public string large_image { get; set; }
        public string large_text { get; set; }
        public string small_image { get; set; }
        public string small_text { get; set; }
    }

    internal class DiscordParty
    {
        public string id { get; set; }
        public int[] size { get; set; }
    }

    internal class DiscordActivity
    {
        public string state { get; set; }
        public string details { get; set; }
        public DiscordTimestamps timestamps { get; set; }
        public DiscordAssets assets { get; set; }
        public DiscordButton[] buttons { get; set; }
        public DiscordParty party { get; set; }
        public bool instance { get; set; }
    }

    internal class DiscordCommandArgs
    {
        public int pid { get; set; }
        public DiscordActivity activity { get; set; }
    }

    internal class DiscordCommand
    {
        public string cmd { get; set; }
        public DiscordCommandArgs args { get; set; }
        public string nonce { get; set; }
    }

    public class DiscordIPC : IDisposable
    {
        private NamedPipeClientStream pipe;
        private bool isConnected = false;
        private bool isDisposed = false;
        private readonly object lockObject = new object();
        private Task keepAliveTask;
        private Task receiveTask;
        private readonly CancellationTokenSource lifecycleCts = new CancellationTokenSource();
        private CancellationTokenSource connectionCts;
        private Task<bool> connectTask;

        private const long DISCORD_CLIENT_ID = 1434950358639181945;
        private const string PIPE_NAME_PREFIX = "discord-ipc-";
        private const int MAX_PIPE_RETRIES = 10;
        private const int PIPE_CONNECT_TIMEOUT_MS = 500;
        private const int HANDSHAKE_READ_TIMEOUT_MS = 4000;
        private const int MAX_FRAME_BYTES = 64 * 1024;
        private static readonly int CurrentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

        private static readonly HashSet<string> TrustedProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Discord",
            "DiscordCanary",
            "DiscordPTB"
        };

        public bool IsConnected
        {
            get
            {
                lock (lockObject)
                {
                    return isConnected && pipe != null && pipe.IsConnected;
                }
            }
        }

        public DiscordIPC()
        {
        }

        public bool Connect()
        {
            if (isDisposed)
                return false;

            try
            {
                return ConnectAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return false;
            }
        }

        public Task<bool> ConnectAsync()
        {
            if (isDisposed)
            {
                return Task.FromResult(false);
            }

            lock (lockObject)
            {
                if (isConnected)
                {
                    return Task.FromResult(true);
                }

                if (connectTask != null && !connectTask.IsCompleted)
                {
                    return connectTask;
                }

                connectTask = Task.Run(() => AttemptConnectionAsync(lifecycleCts.Token), lifecycleCts.Token);
                connectTask.ContinueWith(_ =>
                {
                    lock (lockObject)
                    {
                        connectTask = null;
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);

                return connectTask;
            }
        }

        private bool SendCommand(int opcode, object payload)
        {
            lock (lockObject)
            {
                return SendCommandInternal(pipe, opcode, payload, markDisconnectOnFailure: true);
            }
        }

        private bool SendCommandInternal(NamedPipeClientStream targetPipe, int opcode, object payload, bool markDisconnectOnFailure)
        {
            if (targetPipe == null || !targetPipe.IsConnected)
                return false;

            try
            {
                string json = SimpleJson.Serialize(payload);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                byte[] header = new byte[8];
                BitConverter.GetBytes((uint)opcode).CopyTo(header, 0);
                BitConverter.GetBytes((uint)jsonBytes.Length).CopyTo(header, 4);

                targetPipe.Write(header, 0, 8);
                targetPipe.Write(jsonBytes, 0, jsonBytes.Length);
                targetPipe.Flush();

                return true;
            }
            catch (Exception ex)
            {
                if (markDisconnectOnFailure)
                {
                    lock (lockObject)
                    {
                        isConnected = false;
                    }
                }

                RimCordLogger.Warning("Error sending command: {0}", ex.Message);
                return false;
            }
        }

        public bool UpdatePresence(string state, string details, long? startTimestamp = null, long? endTimestamp = null, string largeImageKey = null, string largeImageText = null, string smallImageKey = null, string smallImageText = null, List<(string Label, string Url)> buttons = null, int? partySize = null, int? partyMax = null)
        {
            if (!IsConnected)
            {
                if (!Connect())
                    return false;
            }

            DiscordButton[] buttonArray = null;
            if (buttons != null && buttons.Count > 0)
            {
                var temp = new List<DiscordButton>(buttons.Count);
                foreach (var button in buttons)
                {
                    if (string.IsNullOrEmpty(button.Label) || string.IsNullOrEmpty(button.Url))
                    {
                        continue;
                    }

                    temp.Add(new DiscordButton
                    {
                        label = button.Label,
                        url = button.Url
                    });
                }

                if (temp.Count > 0)
                {
                    buttonArray = temp.ToArray();
                }
            }

            DiscordTimestamps timestamps = null;
            if (startTimestamp.HasValue || endTimestamp.HasValue)
            {
                timestamps = new DiscordTimestamps
                {
                    start = startTimestamp,
                    end = endTimestamp
                };
            }

            DiscordAssets assets = null;
            if (largeImageKey != null || smallImageKey != null)
            {
                assets = new DiscordAssets
                {
                    large_image = largeImageKey,
                    large_text = largeImageText,
                    small_image = smallImageKey,
                    small_text = smallImageText
                };
            }

            DiscordParty party = null;
            if (partySize.HasValue && partySize.Value >= 1 && partyMax.HasValue)
            {
                party = new DiscordParty
                {
                    id = "rimcord_colony",
                    size = new int[] { partySize.Value, partyMax.Value }
                };
            }

            var activity = new DiscordActivity
            {
                state = state,
                details = details,
                timestamps = timestamps,
                assets = assets,
                buttons = buttonArray,
                party = party,
                instance = true
            };

            var command = new DiscordCommand
            {
                cmd = "SET_ACTIVITY",
                args = new DiscordCommandArgs
                {
                    pid = CurrentProcessId,
                    activity = activity
                },
                nonce = Guid.NewGuid().ToString("N")
            };

            return SendCommand(1, command);
        }

        public bool ClearPresence()
        {
            return UpdatePresence(null, null);
        }

        private async Task KeepAliveLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                try
                {
                    await Task.Delay(30000, cancellationToken);
                    
                    if (IsConnected)
                    {
                        lock (lockObject)
                        {
                            if (pipe != null && pipe.IsConnected)
                            {
                                SendCommandInternal(pipe, 3, new { }, markDisconnectOnFailure: true);
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    RimCordLogger.Warning("Keep-alive error: {0}", ex.Message);
                    isConnected = false;
                    break;
                }
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            lifecycleCts.Cancel();

            lock (lockObject)
            {
                isConnected = false;
            }

            try
            {
                connectionCts?.Cancel();
                keepAliveTask?.Wait(1000);
                receiveTask?.Wait(1000);
            }
            catch { }

            lock (lockObject)
            {
                try
                {
                    pipe?.Dispose();
                    pipe = null;
                }
                catch { }
            }

            connectionCts?.Dispose();
            lifecycleCts.Dispose();
        }

        private bool AttemptConnectionAsync(CancellationToken token)
        {
            for (int i = 0; i < MAX_PIPE_RETRIES; i++)
            {
                if (token.IsCancellationRequested)
                {
                    return false;
                }

                string pipeName = PIPE_NAME_PREFIX + i;
                NamedPipeClientStream candidate = null;

                try
                {
                    candidate = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    candidate.Connect(PIPE_CONNECT_TIMEOUT_MS);
                    candidate.ReadMode = PipeTransmissionMode.Byte;

                    if (!ValidateServerProcess(candidate))
                    {
                        candidate.Dispose();
                        continue;
                    }

                    if (!PerformHandshake(candidate))
                    {
                        candidate.Dispose();
                        continue;
                    }

                    EstablishConnection(candidate, pipeName);
                    return true;
                }
                catch (TimeoutException)
                {
                    candidate?.Dispose();
                }
                catch (Exception ex)
                {
                    candidate?.Dispose();
                    RimCordLogger.Warning("[ORANGE] Failed to connect to pipe {0}: {1}", pipeName, ex.Message);
                }
            }

            RimCordLogger.Warning("Could not connect to Discord. Make sure Discord desktop app is running.");
            return false;
        }

        private void EstablishConnection(NamedPipeClientStream connectedPipe, string pipeName)
        {
            lock (lockObject)
            {
                CancelConnectionLocked();
                pipe = connectedPipe;
                isConnected = true;

                connectionCts = CancellationTokenSource.CreateLinkedTokenSource(lifecycleCts.Token);
                keepAliveTask = Task.Run(() => KeepAliveLoop(connectionCts.Token), connectionCts.Token);
                receiveTask = Task.Run(() => ReceiveLoop(connectedPipe, connectionCts.Token), connectionCts.Token);
                RimCordLogger.Info("Connected to Discord via IPC pipe {0}", pipeName);
            }
        }

        private void ReceiveLoop(NamedPipeClientStream activePipe, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!TryReadFrame(activePipe, out int opcode, out string payload, timeoutMs: -1))
                    {
                        break;
                    }

                    if (opcode == 1)
                    {
                        HandleDispatchFrame(payload);
                    }
                    else if (opcode == 2)
                    {
                        RimCordLogger.Warning("Discord closed IPC channel: {0}", payload ?? "no payload");
                        break;
                    }
                    else if (opcode == 3)
                    {
                        SendCommandInternal(activePipe, 4, new { }, markDisconnectOnFailure: false);
                    }
                }
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Discord IPC receive loop error: {0}", ex.Message);
            }
            finally
            {
                HandleDisconnect();
            }
        }

        private void HandleDispatchFrame(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return;
            }

            if (payload.IndexOf("\"evt\":\"ERROR\"", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Silently ignore transient Discord errors - they're common and not actionable
            }
            else if (payload.IndexOf("\"evt\":\"READY\"", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                RimCordLogger.Info("Discord IPC handshake completed.");
            }
        }

        private bool PerformHandshake(NamedPipeClientStream targetPipe)
        {
            var handshake = new
            {
                v = 1,
                client_id = DISCORD_CLIENT_ID.ToString()
            };

            if (!SendCommandInternal(targetPipe, 0, handshake, markDisconnectOnFailure: false))
            {
                return false;
            }

            if (!TryReadFrame(targetPipe, out int opcode, out string payload, HANDSHAKE_READ_TIMEOUT_MS))
            {
                RimCordLogger.Warning("Discord IPC handshake failed: no response");
                return false;
            }

            if (opcode != 1 || payload.IndexOf("\"evt\":\"READY\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                RimCordLogger.Warning("Discord IPC handshake failed: unexpected payload {0}", payload ?? "<null>");
                return false;
            }

            return true;
        }

        private bool ReadWithOptionalTimeout(NamedPipeClientStream stream, byte[] buffer, int length, int timeoutMs)
        {
            if (timeoutMs < 0)
            {
                return ReadExact(stream, buffer, length);
            }

            if (stream.CanTimeout)
            {
                stream.ReadTimeout = timeoutMs;
                return ReadExact(stream, buffer, length);
            }

            try
            {
                return ReadExactWithCancellation(stream, buffer, length, timeoutMs);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private bool ReadExactWithCancellation(Stream stream, byte[] buffer, int length, int timeoutMs)
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(timeoutMs);
                int read = 0;
                while (read < length)
                {
                    int chunk = stream.ReadAsync(buffer, read, length - read, cts.Token).GetAwaiter().GetResult();
                    if (chunk <= 0)
                    {
                        return false;
                    }
                    read += chunk;
                }

                return true;
            }
        }

        private bool ValidateServerProcess(NamedPipeClientStream targetPipe)
        {
            try
            {
                if (!GetNamedPipeServerProcessId(targetPipe.SafePipeHandle, out uint processId))
                {
                    RimCordLogger.Warning("Unable to determine Discord pipe owner (error {0}).", Marshal.GetLastWin32Error());
                    return false;
                }

                using (var process = Process.GetProcessById((int)processId))
                {
                    string name = process?.ProcessName;
                    if (string.IsNullOrEmpty(name) || !TrustedProcessNames.Contains(name))
                    {
                        RimCordLogger.Warning("Pipe server process '{0}' (PID {1}) is not recognized as Discord. Connection rejected.", name ?? "<unknown>", processId);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                RimCordLogger.Warning("Failed to validate Discord pipe owner: {0}", ex.Message);
                return false;
            }
        }

        private bool TryReadFrame(NamedPipeClientStream targetPipe, out int opcode, out string payload, int timeoutMs)
        {
            opcode = 0;
            payload = null;

            try
            {
                byte[] header = new byte[8];
                if (!ReadWithOptionalTimeout(targetPipe, header, 8, timeoutMs))
                {
                    return false;
                }

                opcode = BitConverter.ToInt32(header, 0);
                int length = BitConverter.ToInt32(header, 4);
                if (length < 0 || length > MAX_FRAME_BYTES)
                {
                    RimCordLogger.Warning("Discord IPC frame length {0} is invalid.", length);
                    return false;
                }

                byte[] buffer = new byte[length];
                if (!ReadWithOptionalTimeout(targetPipe, buffer, length, timeoutMs))
                {
                    return false;
                }

                payload = length == 0 ? string.Empty : Encoding.UTF8.GetString(buffer);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        private bool ReadExact(Stream stream, byte[] buffer, int length)
        {
            int read = 0;
            while (read < length)
            {
                int chunk = stream.Read(buffer, read, length - read);
                if (chunk <= 0)
                {
                    return false;
                }
                read += chunk;
            }

            return true;
        }

        private void HandleDisconnect()
        {
            lock (lockObject)
            {
                if (pipe == null && !isConnected)
                {
                    return;
                }

                isConnected = false;
                CancelConnectionLocked();

                try
                {
                    pipe?.Dispose();
                }
                catch { }

                pipe = null;
                RimCordLogger.Warning("Discord connection lost. Will attempt to reconnect automatically.");
            }
        }

        private void CancelConnectionLocked()
        {
            try
            {
                if (connectionCts != null)
                {
                    connectionCts.Cancel();
                    connectionCts.Dispose();
                    connectionCts = null;
                }
            }
            catch { }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeServerProcessId(SafeHandle pipeHandle, out uint serverProcessId);
    }

    internal static class SimpleJson
    {
        public static string Serialize(object obj)
        {
            if (obj == null)
                return "null";

            var sb = new StringBuilder(256);
            SerializeValue(obj, sb, 0);
            return sb.ToString();
        }

        private const int MaxDepth = 10;

        private static void SerializeValue(object value, StringBuilder sb, int depth)
        {
            if (depth > MaxDepth)
            {
                sb.Append("null");
                return;
            }

            if (value == null)
            {
                sb.Append("null");
                return;
            }

            Type type = value.GetType();

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type);
                if (value == null)
                {
                    sb.Append("null");
                    return;
                }
            }

            if (type == typeof(string))
            {
                sb.Append('"');
                EscapeJsonString((string)value, sb);
                sb.Append('"');
                return;
            }

            if (type == typeof(int) || type == typeof(long) || type == typeof(uint) || type == typeof(ulong) ||
                type == typeof(short) || type == typeof(ushort) || type == typeof(byte) || type == typeof(sbyte))
            {
                sb.Append(value.ToString());
                return;
            }

            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                sb.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            if (type == typeof(bool))
            {
                sb.Append((bool)value ? "true" : "false");
                return;
            }

            if (type.IsArray)
            {
                SerializeArray((Array)value, sb, depth);
                return;
            }

            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                SerializeEnumerable(enumerable, sb, depth);
                return;
            }

            if (type.IsClass && !type.IsPrimitive)
            {
                SerializeObject(value, type, sb, depth);
                return;
            }

            sb.Append('"');
            EscapeJsonString(value.ToString(), sb);
            sb.Append('"');
        }

        private static void SerializeArray(Array array, StringBuilder sb, int depth)
        {
            sb.Append('[');
            bool first = true;
            foreach (var item in array)
            {
                if (!first)
                    sb.Append(',');
                first = false;
                SerializeValue(item, sb, depth + 1);
            }
            sb.Append(']');
        }

        private static void SerializeEnumerable(System.Collections.IEnumerable enumerable, StringBuilder sb, int depth)
        {
            sb.Append('[');
            bool first = true;
            foreach (var item in enumerable)
            {
                if (!first)
                    sb.Append(',');
                first = false;
                SerializeValue(item, sb, depth + 1);
            }
            sb.Append(']');
        }

        private static void SerializeObject(object value, Type type, StringBuilder sb, int depth)
        {
            sb.Append('{');
            var props = type.GetProperties();
            bool first = true;

            foreach (var prop in props)
            {
                if (!prop.CanRead)
                    continue;

                object propValue;
                try
                {
                    propValue = prop.GetValue(value);
                }
                catch
                {
                    continue;
                }

                if (propValue == null)
                    continue;

                if (!first)
                    sb.Append(',');
                first = false;

                sb.Append('"');
                sb.Append(prop.Name);
                sb.Append('"');
                sb.Append(':');
                SerializeValue(propValue, sb, depth + 1);
            }

            sb.Append('}');
        }

        private static void EscapeJsonString(string str, StringBuilder sb)
        {
            if (string.IsNullOrEmpty(str))
                return;

            foreach (char c in str)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    default:
                        if (c < ' ')
                        {
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
        }
    }
}
