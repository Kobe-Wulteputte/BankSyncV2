#!/usr/bin/env bash
# Ships this folder to the VM, rebuilds and restarts the app. Run from Git Bash: bash deploy.sh
# .env and secrets/ on the VM are never touched; everything else there is replaced.
set -euo pipefail

HOST="${DEPLOY_HOST:-kobew@192.168.68.63}"
cd "$(dirname "$0")"

echo "==> Shipping code to $HOST"
tar czf - \
  --exclude=node_modules --exclude=bin --exclude=obj \
  --exclude=client/dist --exclude=src/BS2.Api/wwwroot \
  --exclude=.vs --exclude=.idea --exclude=./.env --exclude=./secrets \
  . | ssh "$HOST" '
    set -e
    tmp=$(mktemp)
    cat > "$tmp"
    [ -s "$tmp" ] || { echo "empty upload, aborting"; exit 1; }
    mkdir -p ~/banksync && cd ~/banksync
    # Clear old code so files deleted locally disappear here too.
    find . -mindepth 1 -maxdepth 1 ! -name .env ! -name secrets -exec rm -rf {} +
    tar --warning=no-unknown-keyword -xzf "$tmp"
    rm -f "$tmp"
  '

echo "==> Building and restarting"
ssh "$HOST" '
  set -e
  cd ~/banksync
  docker compose --profile app up -d --build
  docker image prune -f > /dev/null
  for i in $(seq 1 30); do
    if curl -sf -o /dev/null http://127.0.0.1:8080/; then echo "==> Deployed, app answers"; exit 0; fi
    sleep 2
  done
  echo "==> App did not answer within 60s. Last log lines:"
  docker logs --tail 30 banksync-api
  exit 1
'
