#!/usr/bin/env python3
"""File a report to a repairkit daemon over its Unix socket, the way an in-app collector does.

submit -> attach_photos (optional) -> maybe_auto_fix, so the daemon starts a repair when its auto-repair gate allows.
Prints the report id. Standard library only.

    file_report.py "what is wrong" [--photo shot.png ...] [--context '{"k": "v"}'] [--source schema-editor]
                   [--socket ~/.schema-unity/repairkit/repairkit.sock] [--no-auto]
"""
import argparse
import base64
import json
import mimetypes
import os
import socket
import sys

DEFAULT_SOCKET = "~/.schema-unity/repairkit/repairkit.sock"
MAX_PHOTO_BYTES = 2 * 1024 * 1024


def call(sock_path, request):
    with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as s:
        s.connect(sock_path)
        s.sendall((json.dumps(request) + "\n").encode())
        buf = b""
        while not buf.endswith(b"\n"):
            chunk = s.recv(65536)
            if not chunk:
                break
            buf += chunk
    reply = json.loads(buf)
    if not reply.get("ok"):
        sys.exit(f"file_report: daemon refused {request['op']}: {reply.get('error')}")
    return reply.get("data")


def photo_payload(path):
    data = open(path, "rb").read()
    if len(data) > MAX_PHOTO_BYTES:
        sys.exit(f"file_report: {path} is {len(data)} bytes; the daemon takes at most {MAX_PHOTO_BYTES}")
    mime = mimetypes.guess_type(path)[0] or "image/png"
    return {"name": os.path.basename(path), "data": f"data:{mime};base64," + base64.b64encode(data).decode()}


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("message")
    p.add_argument("--photo", action="append", default=[], help="an image to attach (repeatable, 2 MB each)")
    p.add_argument("--context", default="{}", help="a JSON object of host state")
    p.add_argument("--source", default="schema-editor")
    p.add_argument("--socket", default=DEFAULT_SOCKET)
    p.add_argument("--no-auto", action="store_true", help="file only; do not ask the daemon to start a repair")
    a = p.parse_args(argv)

    sock_path = os.path.expanduser(a.socket)
    context = json.loads(a.context)
    if not isinstance(context, dict):
        sys.exit("file_report: --context must be a JSON object")
    photos = [photo_payload(path) for path in a.photo]

    report_id = call(sock_path, {"op": "submit", "message": a.message, "source": a.source, "context": context})["id"]
    if photos:
        call(sock_path, {"op": "attach_photos", "id": report_id, "photos": photos})
    if not a.no_auto:
        triggered = (call(sock_path, {"op": "maybe_auto_fix", "id": report_id}) or {}).get("triggered")
        print(f"auto-repair asked: triggered={triggered}", file=sys.stderr)
    print(report_id)


if __name__ == "__main__":
    main()
