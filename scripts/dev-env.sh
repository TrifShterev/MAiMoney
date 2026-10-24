#!/usr/bin/env bash
# Brings up a working MAiMoney environment on Linux (Claude Code cloud sessions, WSL, CI):
# the .NET 10 SDK, MongoDB on localhost:27017 and the web app on http://localhost:5166.
#
# Usage: scripts/dev-env.sh [command]
#   up             SDK + MongoDB + app; leaves running pieces alone (default)
#   sdk            install the .NET 10 SDK if it is missing
#   db             make sure MongoDB answers on localhost:27017
#   app            rebuild and restart the web app in the background
#   stop           stop the app and the MongoDB this script started
#   status         show what is running
#   session-start  SessionStart hook entry point; does nothing outside cloud sessions
#
# MongoDB is taken from the first source that works:
#   1. something already listening on localhost:27017
#   2. the `mongo` service from docker-compose.yml (starts dockerd if needed)
#   3. the mongod binary shipped in the EphemeralMongo NuGet runtime package. It comes from
#      api.nuget.org, which cloud sessions can reach; fastdl.mongodb.org usually isn't allowed.
# Force one with MAIMONEY_MONGO=docker or MAIMONEY_MONGO=nuget.
set -uo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
STATE="$ROOT/.dev-env"
MONGO_PORT=27017
APP_PORT=5166
APP_URL="http://localhost:$APP_PORT"
DOTNET_LOCAL="$HOME/.dotnet"
NUGET_MONGO_PACKAGE=ephemeralmongo8.runtime.linux-x64
NUGET_MONGO_VERSION=2.0.0
NUGET_MONGO_DIR="${XDG_CACHE_HOME:-$HOME/.cache}/maimoney/$NUGET_MONGO_PACKAGE.$NUGET_MONGO_VERSION"

# Local sessions (Windows, macOS) keep their own setup; the hook only acts in the cloud.
if [ "${1:-}" = "session-start" ] && [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
if [ -x "$DOTNET_LOCAL/dotnet" ]; then
  export PATH="$DOTNET_LOCAL:$PATH" DOTNET_ROOT="$DOTNET_LOCAL"
fi

mkdir -p "$STATE"

log() { printf '[dev-env] %s\n' "$*" >&2; }

as_root() {
  if [ "$(id -u)" -eq 0 ]; then
    "$@"
  elif command -v sudo >/dev/null 2>&1 && sudo -n true 2>/dev/null; then
    sudo "$@"
  else
    return 1
  fi
}

port_open() { (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null; }

wait_for_port() {
  local port=$1 seconds=$2
  for _ in $(seq "$seconds"); do
    port_open "$port" && return 0
    sleep 1
  done
  return 1
}

# ---------------------------------------------------------------- .NET SDK

has_sdk() { command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; }

ensure_sdk() { install_sdk && ensure_icu; }

install_sdk() {
  has_sdk && return 0

  # Ubuntu 24.04+ carries dotnet-sdk-10.0 in its own archive, which cloud sessions can reach.
  if command -v apt-get >/dev/null 2>&1; then
    log "Installing dotnet-sdk-10.0 with apt..."
    if as_root env DEBIAN_FRONTEND=noninteractive apt-get install -y -q dotnet-sdk-10.0 >"$STATE/apt.log" 2>&1 ||
      { as_root apt-get update -q >>"$STATE/apt.log" 2>&1 &&
        as_root env DEBIAN_FRONTEND=noninteractive apt-get install -y -q dotnet-sdk-10.0 >>"$STATE/apt.log" 2>&1; }; then
      hash -r
      has_sdk && return 0
    fi
    log "apt could not install it (see .dev-env/apt.log)."
  fi

  # No root or no apt: Microsoft's install script into ~/.dotnet (builds.dotnet.microsoft.com may be blocked).
  log "Installing the .NET 10 SDK into ~/.dotnet with dotnet-install.sh..."
  if curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$STATE/dotnet-install.sh" &&
    bash "$STATE/dotnet-install.sh" --channel 10.0 --install-dir "$DOTNET_LOCAL" >"$STATE/dotnet-install.log" 2>&1; then
    export PATH="$DOTNET_LOCAL:$PATH" DOTNET_ROOT="$DOTNET_LOCAL"
    has_sdk && return 0
  fi

  log "ERROR: could not install the .NET 10 SDK (see .dev-env/apt.log and .dev-env/dotnet-install.log)."
  return 1
}

# The apt SDK pulls in libicu; an SDK from dotnet-install.sh on a minimal image doesn't.
ensure_icu() {
  if compgen -G '/usr/lib*/libicuuc.so.*' >/dev/null || compgen -G '/usr/lib/*/libicuuc.so.*' >/dev/null; then
    return 0
  fi

  local package
  package=$(apt-cache search --names-only '^libicu[0-9]+$' 2>/dev/null | sort -V | tail -n 1 | cut -d' ' -f1)
  if [ -n "$package" ] &&
    as_root env DEBIAN_FRONTEND=noninteractive apt-get install -y -q "$package" >>"$STATE/apt.log" 2>&1; then
    return 0
  fi

  # The app formats everything with InvariantCulture, so it works the same without ICU.
  log "libicu is missing and can't be installed; running .NET in globalization-invariant mode."
  export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
}

# ---------------------------------------------------------------- MongoDB

docker_ready() {
  command -v docker >/dev/null 2>&1 || return 1
  docker info >/dev/null 2>&1 && return 0
  command -v dockerd >/dev/null 2>&1 || return 1

  log "Starting the Docker daemon..."
  as_root service docker start >/dev/null 2>&1 ||
    as_root setsid nohup dockerd >"$STATE/dockerd.log" 2>&1 </dev/null &
  for _ in $(seq 30); do
    docker info >/dev/null 2>&1 && return 0
    sleep 1
  done
  log "Docker daemon did not come up (see .dev-env/dockerd.log)."
  return 1
}

mongo_docker() {
  docker_ready || return 1
  log "Starting MongoDB with docker compose..."
  docker compose -f "$ROOT/docker-compose.yml" up -d --wait mongo >"$STATE/compose.log" 2>&1 &&
    wait_for_port "$MONGO_PORT" 30 && return 0
  log "docker compose could not start MongoDB (see .dev-env/compose.log)."
  return 1
}

extract_from_zip() {
  local zip=$1 member=$2 target=$3
  if command -v unzip >/dev/null 2>&1; then
    unzip -p "$zip" "$member" >"$target"
  else
    python3 -c 'import shutil, sys, zipfile
with zipfile.ZipFile(sys.argv[1]) as z, z.open(sys.argv[2]) as src, open(sys.argv[3], "wb") as dst:
    shutil.copyfileobj(src, dst)' "$zip" "$member" "$target"
  fi
}

mongo_nuget() {
  local mongod="$NUGET_MONGO_DIR/mongod"
  if [ ! -x "$mongod" ]; then
    log "Downloading mongod from NuGet ($NUGET_MONGO_PACKAGE $NUGET_MONGO_VERSION, ~77 MB)..."
    mkdir -p "$NUGET_MONGO_DIR"
    local package="$NUGET_MONGO_DIR/package.nupkg"
    local url="https://api.nuget.org/v3-flatcontainer/$NUGET_MONGO_PACKAGE/$NUGET_MONGO_VERSION/$NUGET_MONGO_PACKAGE.$NUGET_MONGO_VERSION.nupkg"
    if ! curl -fsSL -o "$package" "$url" ||
      ! extract_from_zip "$package" runtimes/linux-x64/native/mongodb/bin/mongod "$mongod.part"; then
      rm -f "$package" "$mongod.part"
      log "Could not download mongod from $url"
      return 1
    fi
    rm -f "$package"
    chmod +x "$mongod.part" && mv "$mongod.part" "$mongod"
  fi

  log "Starting mongod from $mongod..."
  mkdir -p "$STATE/mongo-data"
  if ! "$mongod" --dbpath "$STATE/mongo-data" --port "$MONGO_PORT" --bind_ip 127.0.0.1 \
    --logpath "$STATE/mongod.log" --pidfilepath "$STATE/mongod.pid" --fork >"$STATE/mongod-start.log" 2>&1; then
    log "mongod did not start (see .dev-env/mongod-start.log and .dev-env/mongod.log)."
    return 1
  fi
  wait_for_port "$MONGO_PORT" 30
}

ensure_db() {
  if port_open "$MONGO_PORT"; then
    return 0
  fi
  case "${MAIMONEY_MONGO:-}" in
    docker) mongo_docker ;;
    nuget) mongo_nuget ;;
    *) mongo_docker || mongo_nuget ;;
  esac || { log "ERROR: no MongoDB on localhost:$MONGO_PORT."; return 1; }
}

stop_db() {
  if [ -f "$STATE/mongod.pid" ]; then
    kill "$(cat "$STATE/mongod.pid")" 2>/dev/null && log "Stopped mongod."
    rm -f "$STATE/mongod.pid"
  fi
  if command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1; then
    docker compose -f "$ROOT/docker-compose.yml" stop mongo >/dev/null 2>&1 && log "Stopped the MongoDB container."
  fi
}

# ---------------------------------------------------------------- web app

app_responding() { curl -s -o /dev/null --max-time 5 "$APP_URL/"; }

stop_app() {
  if [ -f "$STATE/web.pid" ]; then
    # The app runs in its own process group: `dotnet run` plus the app it launched.
    kill -- "-$(cat "$STATE/web.pid")" 2>/dev/null
    rm -f "$STATE/web.pid"
  fi
  pkill -x MAiMoney.Web 2>/dev/null
  for _ in $(seq 15); do
    port_open "$APP_PORT" || return 0
    sleep 1
  done
}

start_app() {
  stop_app
  log "Building the web app..."
  if ! dotnet build "$ROOT/src/MAiMoney.Web" -nologo -v q >"$STATE/build.log" 2>&1; then
    tail -n 30 "$STATE/build.log" >&2
    log "ERROR: build failed (full output in .dev-env/build.log)."
    return 1
  fi

  log "Starting the web app..."
  (
    cd "$ROOT" || exit 1
    setsid nohup dotnet run --project src/MAiMoney.Web --no-build --launch-profile http \
      >"$STATE/web.log" 2>&1 </dev/null &
    echo $! >"$STATE/web.pid"
  )
  for _ in $(seq 60); do
    app_responding && return 0
    sleep 1
  done
  tail -n 30 "$STATE/web.log" >&2
  log "ERROR: the app did not answer on $APP_URL (full output in .dev-env/web.log)."
  return 1
}

# ---------------------------------------------------------------- commands

status() {
  if has_sdk && ensure_icu 2>/dev/null; then
    echo "dotnet:  $(dotnet --version) ($(command -v dotnet))"
  else
    echo "dotnet:  missing"
  fi
  if port_open "$MONGO_PORT"; then
    echo "mongodb: mongodb://localhost:$MONGO_PORT"
  else
    echo "mongodb: not running"
  fi
  if app_responding; then
    echo "app:     $APP_URL (log: .dev-env/web.log)"
  else
    echo "app:     not running"
  fi
}

up() {
  ensure_sdk || return 1
  ensure_db || return 1
  app_responding || start_app || return 1
}

session_start() {
  up
  local result=$?

  # Later Bash tool calls in this session need the same SDK settings.
  if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
    if [ -x "$DOTNET_LOCAL/dotnet" ]; then
      echo "export PATH=\"$DOTNET_LOCAL:\$PATH\" DOTNET_ROOT=\"$DOTNET_LOCAL\"" >>"$CLAUDE_ENV_FILE"
    fi
    if [ -n "${DOTNET_SYSTEM_GLOBALIZATION_INVARIANT:-}" ]; then
      echo "export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1" >>"$CLAUDE_ENV_FILE"
    fi
  fi

  echo "MAiMoney dev environment (scripts/dev-env.sh, see CLAUDE.md):"
  status
  [ $result -eq 0 ] || echo "Setup failed; see the [dev-env] messages above and the logs in .dev-env/."
  # Never block the session; Claude can fix the environment from here.
  return 0
}

case "${1:-up}" in
  up) up ;;
  sdk) ensure_sdk ;;
  db) ensure_db ;;
  app) ensure_sdk && ensure_db && start_app ;;
  stop) stop_app; stop_db ;;
  status) status ;;
  session-start) session_start ;;
  *)
    sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit 2
    ;;
esac
