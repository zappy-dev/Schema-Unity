#!/bin/bash
# Schema-Unity's repairkit daemon and CLI (docs/RepairKit-Daemon.md). Its own venv, state and socket, all under
# ~/.schema-unity/repairkit/; it never talks to the tank game's daemon.
#
#   tools/repairkit.sh install            make the venv and install repairkit v1.2.1 (and its daemoncore pin) into it
#   tools/repairkit.sh start|stop|restart run the daemon in the background (nohup), or stop it
#   tools/repairkit.sh serve              run the daemon in the foreground (what launchd runs)
#   tools/repairkit.sh running            exit 0 if a daemon answers on the socket
#   tools/repairkit.sh <op> [args]        any repairkit CLI op: ping, health, doctor, list-reports, get-report ID ...
#
# Environment (all optional): SCHEMA_REPAIR_STATE_ROOT (default ~/.schema-unity/repairkit), SCHEMA_REPAIR_BASE
# (default main), SCHEMA_REPAIR_PROJECT_ROOT (default: the primary checkout of this repository),
# SCHEMA_REPAIR_LIBRARY_SEED, SCHEMA_UNITY_CLI. They are read by tools/repair_host.py.
set -euo pipefail

TOOLS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STATE_ROOT="${SCHEMA_REPAIR_STATE_ROOT:-$HOME/.schema-unity/repairkit}"
VENV="${SCHEMA_REPAIR_VENV:-$HOME/.schema-unity/repairkit/venv}"
SOCKET="$STATE_ROOT/repairkit.sock"
PIDFILE="$STATE_ROOT/daemon.pid"
OUTLOG="$STATE_ROOT/daemon.out"
REPAIRKIT_SRC="${REPAIRKIT_SRC:-$HOME/src/repairkit}"
DAEMONCORE_SRC="${DAEMONCORE_SRC:-$HOME/src/daemoncore}"
REPAIRKIT_TAG="v1.2.1"
DAEMONCORE_REV="11597f1c6065068f4b8a738a7a64bba776030b51"   # repairkit v1.2.1's [tool.uv.sources] pin
LAUNCHD_LABEL="com.devzappy.schema-unity.repairkit"

case "$SOCKET" in
  *".tankslop"*) echo "repairkit.sh: refusing $SOCKET: that is the tank game's daemon" >&2; exit 2 ;;
esac

rk() {
  PYTHONPATH="$TOOLS_DIR" "$VENV/bin/repairkit" --host repair_host --socket "$SOCKET" "$@"
}

running() {
  [ -S "$SOCKET" ] && rk ping >/dev/null 2>&1
}

launchd_loaded() {
  launchctl print "gui/$(id -u)/$LAUNCHD_LABEL" >/dev/null 2>&1
}

cmd="${1:-}"
[ -n "$cmd" ] || { sed -n '2,13p' "$0"; exit 2; }
shift

if [ "$cmd" != "install" ] && [ ! -x "$VENV/bin/repairkit" ]; then
  echo "repairkit.sh: no repairkit in $VENV; run: tools/repairkit.sh install" >&2
  exit 2
fi

case "$cmd" in
  install)
    command -v uv >/dev/null || { echo "repairkit.sh: install needs uv (https://docs.astral.sh/uv/)" >&2; exit 2; }
    mkdir -p "$STATE_ROOT"
    [ -x "$VENV/bin/python" ] || uv venv --python 3.12 "$VENV"
    # --no-sources: repairkit's pyproject pins daemoncore by ssh URL; the same commit is taken from the local clone.
    uv pip install --no-sources --python "$VENV/bin/python" \
      "daemoncore @ git+file://$DAEMONCORE_SRC@$DAEMONCORE_REV" \
      "repairkit @ git+file://$REPAIRKIT_SRC@$REPAIRKIT_TAG" \
      claude-agent-sdk \
      pytest
    "$VENV/bin/python" -c 'import importlib.metadata as m; print("repairkit", m.version("repairkit"))'
    ;;
  serve)
    mkdir -p "$STATE_ROOT"
    exec env PYTHONPATH="$TOOLS_DIR" "$VENV/bin/repairkit" --host repair_host serve
    ;;
  start)
    if running; then echo "already running ($SOCKET)"; exit 0; fi
    mkdir -p "$STATE_ROOT"
    cd "$STATE_ROOT"   # not a checkout: nothing the daemon spawns should start in one
    nohup env PYTHONPATH="$TOOLS_DIR" "$VENV/bin/repairkit" --host repair_host serve >>"$OUTLOG" 2>&1 </dev/null &
    echo $! >"$PIDFILE"
    for _ in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20; do
      if running; then echo "started pid $(cat "$PIDFILE") on $SOCKET"; exit 0; fi
      sleep 0.5
    done
    echo "repairkit.sh: the daemon did not answer within 10 s; see $OUTLOG and $STATE_ROOT/repairkit.log" >&2
    exit 1
    ;;
  stop)
    if launchd_loaded; then
      echo "repairkit.sh: launchd owns this daemon; run: launchctl bootout gui/$(id -u)/$LAUNCHD_LABEL" >&2
      exit 1
    fi
    if [ ! -f "$PIDFILE" ]; then
      if running; then echo "repairkit.sh: a daemon answers on $SOCKET but there is no $PIDFILE; stop it by hand" >&2; exit 1; fi
      echo "not running"; exit 0
    fi
    pid="$(cat "$PIDFILE")"
    if kill -0 "$pid" 2>/dev/null; then
      kill -TERM "$pid"
      for _ in $(seq 1 80); do kill -0 "$pid" 2>/dev/null || break; sleep 0.5; done
      if kill -0 "$pid" 2>/dev/null; then echo "repairkit.sh: pid $pid did not exit in 40 s" >&2; exit 1; fi
    fi
    rm -f "$PIDFILE"
    echo "stopped"
    ;;
  restart)
    if launchd_loaded; then exec launchctl kickstart -k "gui/$(id -u)/$LAUNCHD_LABEL"; fi
    "$0" stop
    "$0" start
    ;;
  running)
    running
    ;;
  *)
    rk "$cmd" "$@"
    ;;
esac
