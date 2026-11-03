using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Schema.Unity.Editor.Feedback
{
    internal enum RepairkitFailure
    {
        /// <summary>No daemon answered on the socket (missing file, refused, path too long).</summary>
        Unreachable,

        /// <summary>The connection broke or timed out mid-request.</summary>
        Io,

        /// <summary>The reply was not a repairkit reply, or its protocol is older than this client needs.</summary>
        Protocol,

        /// <summary>The daemon answered <c>ok: false</c>; the message is its error.</summary>
        Daemon,
    }

    internal sealed class RepairkitException : Exception
    {
        public RepairkitFailure Failure { get; }

        /// <summary>True when the daemon never got the request, so a retry later is expected to work.</summary>
        public bool IsConnectionFailure => Failure == RepairkitFailure.Unreachable || Failure == RepairkitFailure.Io;

        public RepairkitException(RepairkitFailure failure, string message, Exception inner = null)
            : base(message, inner)
        {
            Failure = failure;
        }
    }

    /// <summary>
    /// A small client for repairkit's wire: one JSON request line over a Unix socket (<c>{"op": ..., params}</c>
    /// and <c>\n</c>), one JSON reply line (<c>ok</c>, <c>data</c> or <c>error</c>, <c>protocol</c>), a fresh
    /// connection for each op. The contract is repairkit's <c>contract/ops.json</c>. repairkit's generated C#
    /// client uses System.Text.Json, which Unity's runtime does not ship, hence this one on Newtonsoft.
    /// Calls are synchronous with a socket timeout, so call them off the main thread.
    /// </summary>
    internal sealed class RepairkitClient
    {
        /// <summary>Oldest daemon protocol this client talks to.</summary>
        public const int MinProtocol = 1;

        private const int MaxReplyBytes = 16 * 1024 * 1024;

        private readonly string socketPath;
        private readonly int timeoutMs;

        public RepairkitClient(string socketPath, int timeoutMs = FeedbackSettings.DefaultTimeoutMs)
        {
            // A shell expands a leading ~; the socket API does not. Expanding here means every caller --
            // the dialog's test, the sender, the outbox flush -- connects to, and reports, the real path.
            this.socketPath = FeedbackSettings.ExpandUserPath(socketPath);
            this.timeoutMs = timeoutMs;
        }

        /// <summary>Sends one op and returns the reply's <c>data</c>. Null parameter values are left out.</summary>
        public JToken Call(string op, JObject parameters = null)
        {
            var request = new JObject { ["op"] = op };
            if (parameters != null)
            {
                foreach (var property in parameters.Properties())
                {
                    if (property.Value.Type != JTokenType.Null) request[property.Name] = property.Value;
                }
            }

            var payload = Encoding.UTF8.GetBytes(request.ToString(Formatting.None) + "\n");
            var line = Exchange(op, payload);

            JObject reply;
            try
            {
                reply = JObject.Parse(line);
            }
            catch (JsonException e)
            {
                throw new RepairkitException(RepairkitFailure.Protocol, op + ": the daemon's reply was not JSON", e);
            }

            var protocol = reply["protocol"];
            if (protocol != null && protocol.Type == JTokenType.Integer && (int)protocol < MinProtocol)
            {
                throw new RepairkitException(RepairkitFailure.Protocol,
                    op + ": daemon speaks protocol " + (int)protocol + ", this client needs at least " + MinProtocol);
            }

            var ok = reply["ok"];
            if (ok == null || ok.Type != JTokenType.Boolean || !(bool)ok)
            {
                var error = reply["error"];
                throw new RepairkitException(RepairkitFailure.Daemon,
                    error != null && error.Type == JTokenType.String ? (string)error : op + ": unknown daemon error");
            }

            return reply["data"];
        }

        /// <summary>Liveness check; returns the daemon's <c>data</c> (<c>{"status": "ok"}</c>).</summary>
        public JToken Ping() => Call("ping");

        /// <summary>Files a report and returns its id.</summary>
        public string Submit(string message, string source, JObject context)
        {
            var data = Call("submit", new JObject
            {
                ["message"] = message,
                ["source"] = source,
                ["context"] = context ?? new JObject(),
            });
            var id = data?["id"];
            if (id == null || id.Type != JTokenType.String)
            {
                throw new RepairkitException(RepairkitFailure.Protocol, "submit: the reply had no report id");
            }

            return (string)id;
        }

        /// <summary>Attaches images to an existing report. Each photo is at most 2 MB (see <see cref="PhotoFitter"/>).</summary>
        public void AttachPhotos(string reportId, IEnumerable<FeedbackPhoto> photos)
        {
            var array = new JArray();
            foreach (var photo in photos)
            {
                array.Add(new JObject { ["name"] = photo.Name, ["data"] = photo.ToDataUrl() });
            }

            Call("attach_photos", new JObject { ["id"] = reportId, ["photos"] = array });
        }

        /// <summary>
        /// Asks the daemon to start a repair for a report it already has, if its own settings allow one
        /// (<c>repair.auto</c>). repairkit queues nothing by itself: a report stays pending until the collector
        /// sends this, so it goes out once the report and its photos are in. repairkit's reference C# client calls
        /// it <c>MaybeAutoFixAsync</c>. Returns the reply's <c>data</c> (<c>{"triggered": ...}</c>).
        /// </summary>
        public JToken MaybeAutoFix(string reportId) => Call("maybe_auto_fix", new JObject { ["id"] = reportId });

        private string Exchange(string op, byte[] payload)
        {
            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
                {
                    SendTimeout = timeoutMs,
                    ReceiveTimeout = timeoutMs,
                };

                try
                {
                    socket.Connect(new UnixDomainSocketEndPoint(socketPath));
                }
                catch (SocketException e)
                {
                    throw new RepairkitException(RepairkitFailure.Unreachable,
                        op + ": cannot reach the daemon at " + socketPath + " (" + e.Message + ")", e);
                }
                catch (ArgumentException e)
                {
                    // UnixDomainSocketEndPoint refuses a path that does not fit sockaddr_un (104 bytes on macOS)
                    throw new RepairkitException(RepairkitFailure.Unreachable,
                        op + ": not a usable socket path (" + e.Message + ")", e);
                }

                try
                {
                    SendAll(socket, payload);
                    return ReadLine(socket, op);
                }
                catch (SocketException e)
                {
                    throw new RepairkitException(RepairkitFailure.Io, op + ": " + e.Message, e);
                }
                catch (IOException e)
                {
                    throw new RepairkitException(RepairkitFailure.Io, op + ": " + e.Message, e);
                }
                catch (ObjectDisposedException e)
                {
                    throw new RepairkitException(RepairkitFailure.Io, op + ": " + e.Message, e);
                }
            }
            finally
            {
                socket?.Dispose();
            }
        }

        private static void SendAll(Socket socket, byte[] payload)
        {
            var sent = 0;
            while (sent < payload.Length)
            {
                sent += socket.Send(payload, sent, payload.Length - sent, SocketFlags.None);
            }
        }

        private static string ReadLine(Socket socket, string op)
        {
            var collected = new MemoryStream();
            var buffer = new byte[16384];
            while (true)
            {
                var read = socket.Receive(buffer);
                if (read == 0)
                {
                    if (collected.Length == 0)
                    {
                        throw new RepairkitException(RepairkitFailure.Io, op + ": the daemon closed without replying");
                    }

                    break;
                }

                var newline = Array.IndexOf(buffer, (byte)'\n', 0, read);
                if (newline >= 0)
                {
                    collected.Write(buffer, 0, newline);
                    break;
                }

                collected.Write(buffer, 0, read);
                if (collected.Length > MaxReplyBytes)
                {
                    throw new RepairkitException(RepairkitFailure.Protocol, op + ": the reply is larger than " + MaxReplyBytes + " bytes");
                }
            }

            return Encoding.UTF8.GetString(collected.ToArray());
        }
    }
}
