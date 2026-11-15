#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="${DOTNET_ROOT:-/workspace/.dotnet}:$PATH"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/workspace/.dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-/workspace/.nuget/packages}"
docker compose up -d --wait kafka
docker compose run --rm kafka-init
mkdir -p .logs
order_pid=''
stock_pid=''
cleanup() {
  [[ -z "$order_pid" ]] || kill "$order_pid" 2>/dev/null || true
  [[ -z "$stock_pid" ]] || kill "$stock_pid" 2>/dev/null || true
}
trap cleanup EXIT
trap 'exit 130' INT TERM
(cd src/OrderService/OrderService.Api && exec dotnet bin/Debug/net10.0/OrderService.Api.dll --urls http://127.0.0.1:5101) >.logs/order.log 2>&1 &
order_pid=$!
(cd src/StockService/StockService.Api && exec dotnet bin/Debug/net10.0/StockService.Api.dll --urls http://127.0.0.1:5102) >.logs/stock.log 2>&1 &
stock_pid=$!
for attempt in $(seq 1 60); do
  if ! kill -0 "$order_pid" 2>/dev/null || ! kill -0 "$stock_pid" 2>/dev/null; then
    cat .logs/order.log .logs/stock.log
    exit 1
  fi
  if [[ $(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:5101/api/orders/00000000-0000-0000-0000-000000000000) == 404 ]] && curl -fsS http://127.0.0.1:5102/api/stocks/events >/dev/null; then
    echo 'APIs ready. Run: python3 scripts/smoke.py'
    wait -n "$order_pid" "$stock_pid"
    exit 1
  fi
  sleep 1
done
echo 'API startup timed out' >&2
exit 1
