#!/usr/bin/env bash
# Stage the shared Unity-and-Schema fixture (../_fixture) plus this case's overlay as the run's working directory (needs --scaffold).
set -eu
here="$(cd "$(dirname "$0")" && pwd)"
cp -R "$here/../_fixture/." .
if [ -d "$here/overlay" ]; then cp -R "$here/overlay/." .; fi
git init -q -b main
git add -A
git commit -q -m "initial commit"
