#!/usr/bin/env bash
set -euo pipefail

image="${1:?Supply the locally built image tag}"
suffix="${RANDOM}-${RANDOM}"
network="presentation-smoke-${suffix}"
database="presentation-db-${suffix}"
application="presentation-app-${suffix}"
cleanup() {
  docker rm --force "$application" "$database" >/dev/null 2>&1 || true
  docker network rm "$network" >/dev/null 2>&1 || true
}
trap cleanup EXIT
docker network create "$network" >/dev/null
docker run --detach --name "$database" --network "$network" \
  --env POSTGRES_DB=presentation_smoke --env POSTGRES_USER=postgres --env POSTGRES_PASSWORD=disposable-test \
  postgres:18-alpine@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2 >/dev/null
for attempt in $(seq 1 30); do
  if docker exec "$database" pg_isready -U postgres -d presentation_smoke >/dev/null 2>&1; then break; fi
  sleep 1
done
docker exec "$database" pg_isready -U postgres -d presentation_smoke >/dev/null
docker run --detach --name "$application" --network "$network" \
  --env ASPNETCORE_ENVIRONMENT=Production --env Admin__Key=disposable-smoke-admin \
  --env "ConnectionStrings__Presentation=Host=${database};Database=presentation_smoke;Username=postgres;Password=disposable-test;Timeout=3" \
  "$image" >/dev/null
for attempt in $(seq 1 30); do
  if docker exec "$application" wget -q -O /dev/null http://127.0.0.1:8080/health/ready; then break; fi
  sleep 1
done
test "$(docker exec "$application" id -u)" != '0'
docker exec "$application" sh -c 'command -v wget' >/dev/null
docker exec "$application" wget -q -O /dev/null http://127.0.0.1:8080/health/live
docker exec "$application" wget -q -O /dev/null http://127.0.0.1:8080/health/ready
docker stop "$database" >/dev/null
docker exec "$application" wget -q -O /dev/null http://127.0.0.1:8080/health/live
if docker exec "$application" wget -q -O /dev/null http://127.0.0.1:8080/health/ready; then
  echo 'Readiness incorrectly succeeded without PostgreSQL.' >&2
  exit 1
fi
echo 'Container identity, port, health executable, liveness and dependency failure checks passed.'
