#!/usr/bin/env bash
# Build and publish GoGoaRentals (backend + React frontend) on an Ubuntu server.
#
# Usage (as root, from anywhere):
#   bash /opt/gogoarentals-src/deploy/publish.sh            # git pull, build, publish, restart
#   bash /opt/gogoarentals-src/deploy/publish.sh --no-pull  # build what is already checked out
#
# `dotnet publish` also runs `npm install` + `npm run build` in src/Client and copies the
# result into wwwroot, so one .NET app serves both the API and the website.
#
# Override defaults with environment variables, e.g. APP_DIR=/srv/ggr bash publish.sh

set -euo pipefail

SRC_DIR="${SRC_DIR:-/opt/gogoarentals-src}"
APP_DIR="${APP_DIR:-/opt/gogoarentals}"
SERVICE="${SERVICE:-gogoarentals}"
BRANCH="${BRANCH:-main}"
HEALTH_URL="${HEALTH_URL:-http://127.0.0.1:5000/api/health}"

NEW_DIR="${APP_DIR}.new"
OLD_DIR="${APP_DIR}.old"

PULL=1
[[ "${1:-}" == "--no-pull" ]] && PULL=0

step() { echo; echo "==> $*"; }
fail() { echo "ERROR: $*" >&2; exit 1; }

[[ $EUID -eq 0 ]] || fail "run as root (or with sudo)"

# --- prerequisites ----------------------------------------------------------
step "Checking tools"
command -v dotnet >/dev/null || fail "dotnet not found - install with: apt install -y dotnet-sdk-8.0"
command -v node   >/dev/null || fail "node not found - install Node.js 22 (see deployment steps)"
command -v npm    >/dev/null || fail "npm not found - install Node.js 22 (see deployment steps)"
dotnet --list-sdks | grep -q '^8\.' || fail ".NET 8 SDK not installed - apt install -y dotnet-sdk-8.0"
NODE_MAJOR="$(node -p 'process.versions.node.split(".")[0]')"
(( NODE_MAJOR >= 22 )) || fail "Node.js 22+ required, found $(node --version)"
echo "dotnet $(dotnet --version), node $(node --version), npm $(npm --version)"

[[ -f "$SRC_DIR/src/Server/Server.csproj" ]] || fail "source not found at $SRC_DIR (clone the repo there first)"
cd "$SRC_DIR"

# --- source -----------------------------------------------------------------
if (( PULL )); then
  step "Updating source ($BRANCH)"
  git fetch origin "$BRANCH"
  git checkout "$BRANCH"
  git reset --hard "origin/$BRANCH"   # server copy is deploy-only; local edits are discarded
fi
echo "Deploying commit: $(git log -1 --format='%h %s')"

# --- build ------------------------------------------------------------------
step "Publishing to $NEW_DIR (this builds the React app too)"
rm -rf "$NEW_DIR"
dotnet publish src/Server/Server.csproj -c Release -o "$NEW_DIR" --nologo

[[ -f "$NEW_DIR/Server.dll" ]]          || fail "publish output is missing Server.dll"
[[ -f "$NEW_DIR/wwwroot/index.html" ]]  || fail "publish output is missing wwwroot/index.html (React build failed?)"

# Development settings (dev admin password, LocalDB connection) must never run on the server.
rm -f "$NEW_DIR/appsettings.Development.json"

# --- swap in ----------------------------------------------------------------
step "Installing new build to $APP_DIR"
SERVICE_EXISTS=0
systemctl list-unit-files "$SERVICE.service" --no-legend 2>/dev/null | grep -q "$SERVICE" && SERVICE_EXISTS=1

(( SERVICE_EXISTS )) && systemctl stop "$SERVICE"
rm -rf "$OLD_DIR"
[[ -d "$APP_DIR" ]] && mv "$APP_DIR" "$OLD_DIR"
mv "$NEW_DIR" "$APP_DIR"
chown -R root:root "$APP_DIR"

if (( ! SERVICE_EXISTS )); then
  step "Done. Service '$SERVICE' is not set up yet - continue with step 5/6 of the deployment guide."
  exit 0
fi

# --- restart + health check -------------------------------------------------
step "Starting $SERVICE"
systemctl start "$SERVICE"

healthy=0
for _ in $(seq 1 30); do
  if curl -fsS "$HEALTH_URL" 2>/dev/null | grep -q Healthy; then healthy=1; break; fi
  sleep 2
done

if (( healthy )); then
  step "Deployed successfully - $HEALTH_URL is Healthy"
  exit 0
fi

echo "Health check failed. Recent logs:" >&2
journalctl -u "$SERVICE" -n 30 --no-pager >&2 || true

if [[ -d "$OLD_DIR" ]]; then
  step "Rolling back to previous build"
  systemctl stop "$SERVICE"
  rm -rf "$APP_DIR"
  mv "$OLD_DIR" "$APP_DIR"
  systemctl start "$SERVICE"
  fail "new build was unhealthy; previous build restored"
fi
fail "new build is unhealthy and there is no previous build to restore"
