"""Tests for file_report.py against a fake repairkit daemon on a temporary Unix socket.

Run: python3 -m unittest discover -s claude-plugin/skills/repair-loop/scripts -p 'test_*.py'
"""
import base64
import json
import os
import socket
import tempfile
import threading
import unittest

import file_report


class FakeDaemon:
    """Answers one JSON line per connection, records each request, and refuses ops named in `refuse`."""

    def __init__(self, refuse=()):
        self.dir = tempfile.mkdtemp(prefix="fr-", dir="/tmp")  # short: a Unix socket path is limited to 104 chars
        self.path = os.path.join(self.dir, "d.sock")
        self.requests = []
        self.refuse = set(refuse)
        self.server = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        self.server.bind(self.path)
        self.server.listen(8)
        self.thread = threading.Thread(target=self._serve, daemon=True)
        self.thread.start()

    def _serve(self):
        while True:
            try:
                conn, _ = self.server.accept()
            except OSError:
                return
            with conn:
                buf = b""
                while not buf.endswith(b"\n"):
                    chunk = conn.recv(65536)
                    if not chunk:
                        break
                    buf += chunk
                request = json.loads(buf)
                self.requests.append(request)
                if request["op"] in self.refuse:
                    reply = {"ok": False, "error": "refused for the test"}
                elif request["op"] == "submit":
                    reply = {"ok": True, "data": {"id": "r-1"}}
                elif request["op"] == "maybe_auto_fix":
                    reply = {"ok": True, "data": {"triggered": True}}
                else:
                    reply = {"ok": True, "data": {}}
                conn.sendall((json.dumps(reply) + "\n").encode())

    def close(self):
        self.server.close()
        os.unlink(self.path)
        os.rmdir(self.dir)


class FileReportTests(unittest.TestCase):
    def setUp(self):
        self.daemon = FakeDaemon()
        self.addCleanup(self.daemon.close)

    def ops(self):
        return [r["op"] for r in self.daemon.requests]

    def test_submit_then_photo_then_auto_fix_in_that_order(self):
        with tempfile.NamedTemporaryFile(suffix=".png") as shot:
            shot.write(b"\x89PNG fake")
            shot.flush()
            file_report.main(["the tooltip column is cut off", "--photo", shot.name,
                              "--context", '{"package_version": "0.3.2"}', "--socket", self.daemon.path])
        self.assertEqual(self.ops(), ["submit", "attach_photos", "maybe_auto_fix"])
        submit, attach, auto = self.daemon.requests
        self.assertEqual(submit["context"], {"package_version": "0.3.2"})
        self.assertEqual(submit["source"], "schema-editor")
        self.assertEqual(attach["id"], "r-1")
        self.assertEqual(auto["id"], "r-1")
        data = attach["photos"][0]["data"]
        self.assertTrue(data.startswith("data:image/png;base64,"))
        self.assertEqual(base64.b64decode(data.split(",", 1)[1]), b"\x89PNG fake")

    def test_no_photo_skips_attach(self):
        file_report.main(["x", "--socket", self.daemon.path])
        self.assertEqual(self.ops(), ["submit", "maybe_auto_fix"])

    def test_no_auto_files_only(self):
        file_report.main(["x", "--socket", self.daemon.path, "--no-auto"])
        self.assertEqual(self.ops(), ["submit"])

    def test_a_refusal_exits_with_the_daemon_s_reason(self):
        self.daemon.refuse = {"submit"}
        with self.assertRaises(SystemExit) as caught:
            file_report.main(["x", "--socket", self.daemon.path])
        self.assertIn("refused for the test", str(caught.exception))

    def test_context_must_be_an_object(self):
        with self.assertRaises(SystemExit):
            file_report.main(["x", "--context", "[1, 2]", "--socket", self.daemon.path])
        self.assertEqual(self.ops(), [])

    def test_a_photo_over_2_mb_is_refused_before_anything_is_sent(self):
        with tempfile.NamedTemporaryFile(suffix=".png") as big:
            big.write(b"\0" * (2 * 1024 * 1024 + 1))
            big.flush()
            with self.assertRaises(SystemExit):
                file_report.main(["x", "--photo", big.name, "--socket", self.daemon.path])
        self.assertEqual(self.ops(), [])


if __name__ == "__main__":
    unittest.main()
