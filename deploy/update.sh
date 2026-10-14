#!/usr/bin/env bash
# GuessTheLyrics updater: installs the newest GitHub release and rolls back when it does not come up.
#
#   update.sh            install the latest release if it is new (run every 5 minutes by the systemd timer)
#   update.sh v1.2.3     install exactly that version and pin it (rollback); the timer then leaves it alone
#   update.sh latest     unpin and (re)install the latest release, also when it is already installed
#
# Per update: back up the database, set APP_VERSION in .env, pull + restart, wait until
# /api/system/version reports the new version. If it doesn't within HEALTH_TIMEOUT seconds, the previous
# version and the database backup are restored and the failed version is skipped from then on.
set -euo pipefail

APP_DIR="${APP_DIR:-/opt/guessthelyrics}"
REPO="${REPO:-jochemKok132/GuessTheLyrics}"
DB_NAME="GuessTheLyrics"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-180}"
UPDATER_ENV="/etc/guessthelyrics/updater.env"

ENV_FILE="$APP_DIR/.env"
STATE_DIR="$APP_DIR/state"
FAILED_FILE="$STATE_DIR/failed-versions"
PIN_FILE="$STATE_DIR/pinned"

# Optional GITHUB_TOKEN for a private repository.
if [ -f "$UPDATER_ENV" ]; then
    # shellcheck disable=SC1090
    source "$UPDATER_ENV"
fi

log() { echo "[$(date -Is)] $*"; }

for tool in docker curl jq flock; do
    command -v "$tool" >/dev/null || { log "Missing '$tool'. Install it first."; exit 1; }
done
[ -f "$ENV_FILE" ] || { log "No $ENV_FILE. Copy deploy/.env.example there first."; exit 1; }

mkdir -p "$STATE_DIR" "$APP_DIR/backups"
touch "$FAILED_FILE"

# Never run two updates at the same time (timer + manual run).
exec 9>"$STATE_DIR/update.lock"
flock -n 9 || { log "Another update is already running."; exit 0; }

compose() { docker compose --project-directory "$APP_DIR" -f "$APP_DIR/docker-compose.yml" --env-file "$ENV_FILE" "$@"; }

get_env() { grep -E "^$1=" "$ENV_FILE" | tail -n 1 | cut -d= -f2- || true; }

set_env() {
    if grep -qE "^$1=" "$ENV_FILE"; then
        sed -i "s|^$1=.*|$1=$2|" "$ENV_FILE"
    else
        echo "$1=$2" >> "$ENV_FILE"
    fi
}

health_url() { echo "http://127.0.0.1:$(get_env API_PORT | grep . || echo 5090)/api/system/version"; }

# Runs a query in the db container; the sa password stays inside the container.
sql() {
    # shellcheck disable=SC2016  # $MSSQL_SA_PASSWORD and $1 are expanded inside the container shell
    compose exec -T db sh -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -h -1 -W -Q "$1"' sh "$1"
}

latest_tag() {
    local auth=()
    if [ -n "${GITHUB_TOKEN:-}" ]; then auth=(-H "Authorization: Bearer $GITHUB_TOKEN"); fi
    # /releases/latest skips drafts and pre-releases.
    curl -fsSL "${auth[@]}" -H "Accept: application/vnd.github+json" \
        "https://api.github.com/repos/$REPO/releases/latest" | jq -r '.tag_name // empty'
}

# Prints the backup path, or nothing when there is no database yet (first install).
backup_db() {
    local label="$1"
    if ! compose ps --status running --services 2>/dev/null | grep -qx db; then
        log "Database container is not running; no backup needed." >&2
        return 0
    fi

    local exists
    exists="$(sql "SET NOCOUNT ON; SELECT CASE WHEN DB_ID('$DB_NAME') IS NULL THEN 0 ELSE 1 END")" || return 1
    exists="$(echo "$exists" | tr -d '[:space:]')"
    if [ "$exists" != "1" ]; then
        log "Database $DB_NAME does not exist yet; no backup needed." >&2
        return 0
    fi

    local file
    file="/var/opt/mssql/backups/${DB_NAME}-${label}-$(date +%Y%m%d-%H%M%S).bak"
    sql "BACKUP DATABASE [$DB_NAME] TO DISK = N'$file' WITH INIT" >&2 || return 1
    log "Database backed up to backups/$(basename "$file")" >&2
    echo "$file"
}

restore_db() {
    local file="$1"
    log "Restoring database from backups/$(basename "$file")"
    compose stop api
    sql "ALTER DATABASE [$DB_NAME] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$DB_NAME] FROM DISK = N'$file' WITH REPLACE; ALTER DATABASE [$DB_NAME] SET MULTI_USER;"
}

wait_for_version() {
    local want="$1" url got="" deadline=$((SECONDS + HEALTH_TIMEOUT))
    url="$(health_url)"
    while (( SECONDS < deadline )); do
        got="$(curl -fsS --max-time 5 "$url" 2>/dev/null | jq -r '.value.version // empty' 2>/dev/null || true)"
        if [ "$got" = "$want" ]; then return 0; fi
        sleep 5
    done
    log "Health check: expected $want from $url, last answer '${got:-none}'."
    return 1
}

install_version() {
    local target="$1" previous backup
    previous="$(get_env APP_VERSION)"
    log "Installing $target (running: ${previous:-nothing yet})"

    if ! backup="$(backup_db "${previous:-none}")"; then
        log "Database backup failed; not updating."
        return 1
    fi

    set_env APP_VERSION "$target"
    if compose pull && compose up -d --remove-orphans && wait_for_version "$target"; then
        log "Now running $target."
        docker image prune -f >/dev/null || true
        return 0
    fi

    log "Version $target did not come up. Rolling back to ${previous:-nothing}."
    grep -qxF "$target" "$FAILED_FILE" || echo "$target" >> "$FAILED_FILE"

    if [ -z "$previous" ]; then
        # Nothing ran before: clear APP_VERSION again, otherwise the next run thinks $target is installed.
        set_env APP_VERSION ""
        log "There is no previous version to roll back to; check: docker compose logs api"
        return 1
    fi

    set_env APP_VERSION "$previous"
    if [ -n "$backup" ]; then restore_db "$backup"; fi
    compose up -d --remove-orphans
    if wait_for_version "$previous"; then
        log "Rolled back to $previous."
    else
        log "Rollback to $previous did not report healthy either; check: docker compose logs api"
    fi
    return 1
}

install_latest() {
    local tag target
    tag="$(latest_tag)" || { log "Could not reach the GitHub API."; return 1; }
    if [ -z "$tag" ]; then log "No published release yet."; return 0; fi
    target="${tag#v}"

    # Already installed: stay quiet. 'update.sh latest' (force) reinstalls it, e.g. after a failed first install.
    if [ "${1:-}" != "force" ] && [ "$target" = "$(get_env APP_VERSION)" ]; then return 0; fi
    if [ "${1:-}" != "force" ] && grep -qxF "$target" "$FAILED_FILE"; then
        log "Skipping $target: it failed before. Install it anyway with: update.sh latest"
        return 0
    fi

    install_version "$target"
}

case "${1:-}" in
    "")
        if [ -f "$PIN_FILE" ]; then exit 0; fi   # pinned: leave it alone until 'update.sh latest'
        install_latest
        ;;
    latest)
        rm -f "$PIN_FILE"
        install_latest force
        ;;
    v[0-9]* | [0-9]*)
        target="${1#v}"
        install_version "$target"
        echo "$target" > "$PIN_FILE"
        log "Pinned to $target. Run 'update.sh latest' to follow new releases again."
        ;;
    *)
        echo "Usage: $0 [vX.Y.Z | latest]" >&2
        exit 2
        ;;
esac