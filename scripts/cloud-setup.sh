#!/bin/bash
# Setup script for the Claude Code cloud environment (claude.ai/code > environment > Setup script).
# Paste this file there or load it with "Choose file". It runs as root on Ubuntu 24.04 before
# Claude starts, and what it installs is kept in the environment cache, so sessions start fast.
# It is optional: the SessionStart hook (scripts/dev-env.sh session-start) installs anything
# still missing, just more slowly and in every new session.
# Must exit 0 and finish in about five minutes, so every step is allowed to fail.

export DEBIAN_FRONTEND=noninteractive

# .NET 10 SDK from Ubuntu's own archive.
(apt-get update -q && apt-get install -y -q dotnet-sdk-10.0) || echo "dotnet-sdk-10.0 install failed" >&2

# MongoDB image for docker compose. The cache keeps pulled images, not running containers.
if command -v docker >/dev/null 2>&1; then
  if ! docker info >/dev/null 2>&1; then
    service docker start >/dev/null 2>&1 || (setsid nohup dockerd >/var/log/dockerd.log 2>&1 </dev/null &)
    for _ in $(seq 30); do docker info >/dev/null 2>&1 && break; sleep 1; done
  fi
  docker pull mongo:8 || echo "docker pull mongo:8 failed" >&2
fi

exit 0
