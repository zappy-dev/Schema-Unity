using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Schema.Unity.Editor.Feedback.Tests
{
    /// <summary>
    /// A stand-in for the repairkit daemon: a Unix socket server that speaks the wire the real one does
    /// (one JSON request line in, one JSON reply line out, a fresh connection per op) and records every
    /// request it was sent (<c>ping</c>, <c>submit</c>, <c>attach_photos</c>, <c>maybe_auto_fix</c>).
    /// Socket paths must stay under 104 characters on macOS, so tests use
    /// <see cref="NewSocketPath"/>, which is a short name directly under /tmp.
    /// </summary>
    internal sealed class FakeRepairkitServer : IDisposable
    {
        private readonly Socket listener;
        private readonly Thread thread;
        private readonly object gate = new object();
        private readonly List<JObject> requests = new List<JObject>();
        private readonly List<ReceivedPhoto> photos = new List<ReceivedPhoto>();
        private int nextReportNumber;
        private volatile bool stopping;

        /// <summary>Reply ok:false to this many attach_photos calls before accepting one.</summary>
        public int AttachFailuresRemaining;

        /// <summary>Reply ok:false to every maybe_auto_fix call, like a daemon that cannot start a repair.</summary>
        public bool FailAutoFix;

        public string SocketPath { get; }

        public sealed class ReceivedPhoto
        {
            public string ReportId;
            public string Name;
            public string Mime;
            public byte[] Bytes;
        }

        public static string NewSocketPath() =>
            "/tmp/sk-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".sock";

        public FakeRepairkitServer(string socketPath = null)
        {
            SocketPath = socketPath ?? NewSocketPath();
            if (File.Exists(SocketPath)) File.Delete(SocketPath);
            listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
            listener.Listen(16);
            thread = new Thread(Loop) { IsBackground = true, Name = "FakeRepairkitServer" };
            thread.Start();
        }

        public List<JObject> Requests()
        {
            lock (gate) return new List<JObject>(requests);
        }

        public List<JObject> Requests(string op)
        {
            var found = new List<JObject>();
            foreach (var request in Requests())
            {
                if ((string)request["op"] == op) found.Add(request);
            }

            return found;
        }

        public List<ReceivedPhoto> Photos()
        {
            lock (gate) return new List<ReceivedPhoto>(photos);
        }

        private void Loop()
        {
            while (!stopping)
            {
                Socket client;
                try
                {
                    client = listener.Accept();
                }
                catch (Exception)
                {
                    return; // listener closed
                }

                try
                {
                    using (client)
                    {
                        client.ReceiveTimeout = 5000;
                        Serve(client);
                    }
                }
                catch (Exception)
                {
                    // a client that vanished mid-request is not the server's problem
                }
            }
        }

        private void Serve(Socket client)
        {
            var line = ReadLine(client);
            if (line == null) return;

            JObject request;
            try
            {
                request = JObject.Parse(line);
            }
            catch (Exception)
            {
                Reply(client, new JObject { ["ok"] = false, ["error"] = "bad json", ["protocol"] = 2 });
                return;
            }

            lock (gate) requests.Add(request);

            JObject reply;
            switch ((string)request["op"])
            {
                case "ping":
                    reply = Ok(new JObject { ["status"] = "ok" });
                    break;
                case "submit":
                    reply = Ok(new JObject
                    {
                        ["id"] = "r-" + Interlocked.Increment(ref nextReportNumber).ToString(),
                        ["message"] = request["message"],
                        ["source"] = request["source"],
                    });
                    break;
                case "attach_photos":
                    reply = AttachPhotos(request);
                    break;
                case "maybe_auto_fix":
                    reply = FailAutoFix
                        ? new JObject { ["ok"] = false, ["error"] = "repair queue unavailable", ["protocol"] = 2 }
                        : Ok(new JObject { ["id"] = request["id"], ["triggered"] = true });
                    break;
                default:
                    reply = new JObject { ["ok"] = false, ["error"] = "unknown op", ["protocol"] = 2 };
                    break;
            }

            Reply(client, reply);
        }

        private JObject AttachPhotos(JObject request)
        {
            if (Interlocked.Decrement(ref AttachFailuresRemaining) >= 0)
            {
                return new JObject { ["ok"] = false, ["error"] = "photo store unavailable", ["protocol"] = 2 };
            }

            Interlocked.Exchange(ref AttachFailuresRemaining, 0);
            var id = (string)request["id"];
            foreach (var photo in (JArray)request["photos"])
            {
                var data = (string)photo["data"];
                var comma = data.IndexOf(',');
                var header = data.Substring(0, comma); // data:image/png;base64
                var mime = header.Substring("data:".Length).Split(';')[0];
                lock (gate)
                {
                    photos.Add(new ReceivedPhoto
                    {
                        ReportId = id,
                        Name = (string)photo["name"],
                        Mime = mime,
                        Bytes = Convert.FromBase64String(data.Substring(comma + 1)),
                    });
                }
            }

            return Ok(new JObject { ["id"] = id });
        }

        private static JObject Ok(JToken data) =>
            new JObject { ["ok"] = true, ["data"] = data, ["protocol"] = 2 };

        private static string ReadLine(Socket client)
        {
            var bytes = new List<byte>(4096);
            var buffer = new byte[65536];
            while (true)
            {
                var read = client.Receive(buffer);
                if (read == 0) return bytes.Count == 0 ? null : Encoding.UTF8.GetString(bytes.ToArray());
                for (var i = 0; i < read; i++)
                {
                    if (buffer[i] == (byte)'\n') return Encoding.UTF8.GetString(bytes.ToArray());
                    bytes.Add(buffer[i]);
                }
            }
        }

        private static void Reply(Socket client, JObject reply)
        {
            client.Send(Encoding.UTF8.GetBytes(reply.ToString(Newtonsoft.Json.Formatting.None) + "\n"));
        }

        public void Dispose()
        {
            stopping = true;
            try { listener.Close(); } catch (Exception) { /* already closed */ }
            thread.Join(2000);
            try { if (File.Exists(SocketPath)) File.Delete(SocketPath); } catch (IOException) { /* best effort */ }
        }
    }
}
