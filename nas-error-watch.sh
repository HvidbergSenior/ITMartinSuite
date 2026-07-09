#!/bin/bash
# Watches all running containers for unhandled exceptions, fatal errors, and
# crash-loops, and pushes a notification via ntfy.sh when it finds something new.
# Deliberately does NOT match on generic "fail:"/"error" lines - EF Core's
# "ALTER TABLE ... already exists" startup noise (several apps retry a column
# add on every boot) would otherwise spam an alert every single run.
#
# Install (run once on the NAS, via DSM Task Scheduler - see README note below):
#   */15 * * * * /volume1/homes/MartinHvidberg/martinsuite-magic/nas-error-watch.sh >> /volume1/homes/MartinHvidberg/error-watch.log 2>&1
#
# Test once by hand first:
#   NTFY_TOPIC=itmartin-nas-68drx9n3av1g ./nas-error-watch.sh

set -u

NTFY_TOPIC="${NTFY_TOPIC:-itmartin-nas-68drx9n3av1g}"
STATE_DIR="/volume1/homes/MartinHvidberg/nas-alert-state"
mkdir -p "$STATE_DIR"

log() { echo "[$(date -u '+%Y-%m-%d %H:%M:%S UTC')] $*"; }

notify() {
    local title="$1" body="$2"
    curl -s -H "Title: $title" -H "Priority: high" -H "Tags: warning" \
        -d "$body" "https://ntfy.sh/$NTFY_TOPIC" > /dev/null
}

# ── 1. New unhandled exceptions / fatal errors per container ────────────────

for c in $(docker ps --format '{{.Names}}'); do
    state_file="$STATE_DIR/lastcheck-$c"
    since="5m"
    [[ -f "$state_file" ]] && since=$(cat "$state_file")
    now_marker=$(date -u '+%Y-%m-%dT%H:%M:%SZ')

    hits=$(docker logs "$c" --since "$since" 2>&1 | grep -iE "unhandled exception|fatal|outofmemory|System\.NullReferenceException" | head -5)

    if [[ -n "$hits" ]]; then
        first_line=$(echo "$hits" | head -1 | cut -c1-200)
        log "$c: new error - $first_line"
        notify "NAS Alert: $c" "$first_line"
    fi

    echo "$now_marker" > "$state_file"
done

# ── 2. Crash-loop detection (restart count increased since last run) ────────

restart_state="$STATE_DIR/restart-counts"
touch "$restart_state"

for c in $(docker ps -a --format '{{.Names}}'); do
    rc=$(docker inspect "$c" --format '{{.RestartCount}}' 2>/dev/null || echo 0)
    prev=$(grep "^$c=" "$restart_state" | cut -d= -f2)
    prev="${prev:-0}"

    if [[ "$rc" -gt "$prev" ]]; then
        log "$c: restart count $prev -> $rc"
        notify "NAS Alert: $c restarted" "Restart count went from $prev to $rc - container may be crash-looping."
    fi

    grep -v "^$c=" "$restart_state" > "$restart_state.tmp" 2>/dev/null || true
    echo "$c=$rc" >> "$restart_state.tmp"
    mv "$restart_state.tmp" "$restart_state"
done
