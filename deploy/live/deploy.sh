#!/usr/bin/env bash
# Run on the server by .github/workflows/deploy-live.yml: backs up the database, starts the given image, checks that the server really
# came up, and puts the previous version back if it did not.
# Usage: ./deploy.sh ghcr.io/rauseklis/openmu:<commit>      (HEALTH_TIMEOUT=<seconds> changes how long it waits; default 300)
set -euo pipefail

cd "$(dirname "$0")"
image="${1:?usage: deploy.sh <image>}"
timeout="${HEALTH_TIMEOUT:-300}"
previous="$(grep '^OPENMU_IMAGE=' .env | cut -d= -f2- || true)"

# Keep the image in .env, so that a plain `docker compose up -d` (after a reboot) starts the same version.
set_image() {
    if grep -q '^OPENMU_IMAGE=' .env; then
        sed -i "s|^OPENMU_IMAGE=.*|OPENMU_IMAGE=$1|" .env
    else
        echo "OPENMU_IMAGE=$1" >> .env
    fi
}

# The admin panel answers (200, or a redirect to its login) only when the application has finished starting. A port check does not prove
# that: Docker accepts connections on a published port while the application is still starting, or after it crashed.
healthy() {
    local running code
    running="$(docker inspect -f '{{.State.Running}}' openmu 2>/dev/null || echo false)"
    code="$(curl -s -o /dev/null -m 5 -w '%{http_code}' http://127.0.0.1:8080/ || true)"
    [ "$running" = "true" ] && { [ "$code" = "200" ] || [ "$code" = "301" ] || [ "$code" = "302" ]; }
}

wait_healthy() {
    local seconds="$1" waited=0
    while [ "$waited" -lt "$seconds" ]; do
        if healthy; then return 0; fi
        sleep 5
        waited=$((waited + 5))
    done
    return 1
}

# The backup comes first: if it fails nothing has been changed yet.
./backup.sh

set_image "$image"

if ! docker compose pull openmu; then
    echo "The image $image could not be pulled: nothing was changed, the running version stays." >&2
    set_image "${previous:-$image}"
    exit 1
fi

docker compose up -d

if wait_healthy "$timeout"; then
    docker image prune -f > /dev/null
    echo "deployed ${image}: the server is up (the admin panel answers)"
    exit 0
fi

echo "The server did not come up within ${timeout} seconds with ${image}." >&2
docker compose logs --tail 40 openmu >&2 || true

if [ -n "$previous" ] && [ "$previous" != "$image" ]; then
    echo "Rolling back to the previous version: ${previous}" >&2
    set_image "$previous"
    docker compose up -d openmu
    if wait_healthy 180; then
        echo "Rolled back: the previous version is running again. The new version ${image} is NOT live." >&2
    else
        echo "The rollback did not come up either. Look at: docker compose logs openmu" >&2
    fi
fi
exit 1
