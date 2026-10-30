#!/usr/bin/env bash
# Run on the server by .github/workflows/deploy-live.yml: backs up the database, starts the given image, checks that the server really
# came up, and puts the previous version back if it did not.
# Usage: ./deploy.sh ghcr.io/rauseklis/openmu:<commit>      (HEALTH_TIMEOUT=<seconds> changes how long it waits; default 300;
# STABLE_SECONDS=<seconds> is how long everything must stay healthy without a gap before success is declared; default 30)
set -euo pipefail

cd "$(dirname "$0")"
image="${1:?usage: deploy.sh <image>}"
timeout="${HEALTH_TIMEOUT:-300}"
stable_seconds="${STABLE_SECONDS:-30}"
# The connect server, the game servers and the chat server (the published ports of docker-compose.yml).
game_ports="${GAME_PORTS:-44405 44406 55901 55902 55903 55904 55905 55906 55980}"
previous="$(grep '^OPENMU_IMAGE=' .env | cut -d= -f2- || true)"

# Keep the image in .env, so that a plain `docker compose up -d` (after a reboot) starts the same version.
set_image() {
    if grep -q '^OPENMU_IMAGE=' .env; then
        sed -i "s|^OPENMU_IMAGE=.*|OPENMU_IMAGE=$1|" .env
    else
        echo "OPENMU_IMAGE=$1" >> .env
    fi
}

# The admin panel answers (200, or a redirect to its login) only when the application has finished starting. A port check alone does not
# prove that: Docker accepts connections on a published port while the application is still starting, or after it crashed. So both are
# required: the admin panel AND every game-facing port (a web process can be fine while the game servers are down).
healthy() {
    local running code port
    running="$(docker inspect -f '{{.State.Running}}' openmu 2>/dev/null || echo false)"
    [ "$running" = "true" ] || return 1
    code="$(curl -s -o /dev/null -m 5 -w '%{http_code}' http://127.0.0.1:8080/ || true)"
    if [ "$code" != "200" ] && [ "$code" != "301" ] && [ "$code" != "302" ]; then
        return 1
    fi
    for port in $game_ports; do
        if ! timeout 3 bash -c "exec 3<>/dev/tcp/127.0.0.1/$port" 2>/dev/null; then
            echo "port $port does not accept connections yet" >&2
            return 1
        fi
    done
}

# Healthy means: healthy for stable_seconds in a row. A server that answers once and then crashes (or restarts in a loop) is not healthy.
wait_healthy() {
    local seconds="$1" waited=0 stable_since=-1
    while [ "$waited" -lt "$seconds" ]; do
        if healthy; then
            [ "$stable_since" -ge 0 ] || stable_since="$waited"
            if [ $((waited - stable_since)) -ge "$stable_seconds" ]; then return 0; fi
        else
            stable_since=-1
        fi
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
    echo "deployed ${image}: the admin panel and all game ports stayed healthy for ${stable_seconds} seconds"
    exit 0
fi

echo "The server did not come up within ${timeout} seconds with ${image}." >&2
docker compose logs --tail 40 openmu >&2 || true

if [ -n "$previous" ] && [ "$previous" != "$image" ]; then
    echo "Rolling back to the previous version: ${previous}" >&2
    set_image "$previous"
    # The compose file and backup script of the previous deployment (saved by the workflow before it copied the new ones) go back together
    # with the image, so a compose change cannot survive the rollback. deploy.sh itself is left alone: it is running right now.
    if [ -f previous/docker-compose.yml ]; then
        cp -f previous/docker-compose.yml docker-compose.yml
        [ -f previous/backup.sh ] && cp -f previous/backup.sh backup.sh
    fi
    docker compose up -d openmu
    if wait_healthy 180; then
        echo "Rolled back: the previous version is running again. The new version ${image} is NOT live." >&2
    else
        echo "The rollback did not come up either. Look at: docker compose logs openmu" >&2
    fi
fi
exit 1
