#!/bin/bash
# Auto-stops idle manual-profile containers on the NAS to free up RAM.
# Runs via cron. Read from Stats' /api/last-seen where an app has a public
# domain; falls back to container uptime for internal/local-only tools that
# don't get tracked. Logs every action it takes.
#
# Install (run once on the NAS):
#   crontab -e
#   */30 * * * * /volume1/homes/MartinHvidberg/martinsuite-magic/nas-idle-stop.sh >> /volume1/homes/MartinHvidberg/idle-stop.log 2>&1
#
# Dry run first to see what it WOULD stop, without stopping anything:
#   DRY_RUN=1 ./nas-idle-stop.sh

set -u

IDLE_HOURS="${IDLE_HOURS:-4}"
IDLE_SECONDS=$((IDLE_HOURS * 3600))
STATS_URL="http://localhost:8115/api/last-seen"
COMPOSE_DIR="/volume1/homes/MartinHvidberg/martinsuite-magic"
DRY_RUN="${DRY_RUN:-0}"

log() { echo "[$(date -u '+%Y-%m-%d %H:%M:%S UTC')] $*"; }

# Never touch these — real external shop use, core infra, or the tracking
# mechanism itself. Everything else with profiles:[manual] is fair game,
# including "heavy" apps like magic-web — idle is idle.
EXCLUDE=(receipt-web library-web index-web cloudflared rabbitmq stats-web magic-postgres cloudoverblik-web)

# service -> public domain (as tracked in Stats' Host column). Services with
# no public domain (internal/local-only tools) are left out here and fall
# back to the uptime check below.
declare -A SERVICE_HOST=(
    [magic-web]="magic-card-pricing.itmartin.dk"
    [gallery-web]="gallery.itmartin.dk"
    [filesorter-web]="sort.itmartin.dk"
    [budget-web]="budget.itmartin.dk"
    [library-search-web]="search-books.itmartin.dk"
    [family-web]="familie.itmartin.dk"
    [adhd-web]="adhd.itmartin.dk"
    [bartab-web]="bartab.itmartin.dk"
    [auction-web]="auction.itmartin.dk"
    [market-web]="market.itmartin.dk"
    [imagegen-web]="billedbehandling.itmartin.dk"
    [cloudoverblik-web]="cloudoverblik.itmartin.dk"
    [testhub-web]="test.itmartin.dk"
    [club-web]="lions-club.itmartin.dk"
    [musik-web]="musik.itmartin.dk"
    [live-web]="live.itmartin.dk"
    [scan-web]="scan.itmartin.dk"
    [upload-web]="upload.itmartin.dk"
    [poll-web]="stem.itmartin.dk"
    [uret-web]="uretfaerdighed.itmartin.dk"
    [stream-web]="stream.itmartin.dk"
)

is_excluded() {
    local svc="$1"
    for e in "${EXCLUDE[@]}"; do [[ "$svc" == "$e" ]] && return 0; done
    return 1
}

is_running() {
    docker ps --filter "name=^/${1}$" --filter status=running -q | grep -q .
}

last_seen_epoch() {
    # $1 = host. Prints epoch seconds of last hit, or empty if none/unavailable.
    curl -s --max-time 5 "$STATS_URL" 2>/dev/null | \
        grep -o "\"$1\":\"[^\"]*\"" | sed -E "s/.*\"([^\"]+)\"$/\1/" | \
        xargs -I{} date -u -d {} +%s 2>/dev/null
}

container_started_epoch() {
    docker inspect -f '{{.State.StartedAt}}' "$1" 2>/dev/null | xargs -I{} date -u -d {} +%s 2>/dev/null
}

stop_service() {
    local svc="$1"
    if [[ "$DRY_RUN" == "1" ]]; then
        log "DRY RUN — would stop: $svc"
    else
        log "Stopping idle service: $svc"
        (cd "$COMPOSE_DIR" && docker compose stop "$svc") >> /dev/null
    fi
}

now=$(date -u +%s)

# Every service currently in docker-compose with profiles:[manual], minus EXCLUDE.
ALL_MANAGED=(index-web filesorter-web filesorter-worker curator-web gallery-web
    gallery-mie gallery-hvidbergfamily gallery-hvidberg budget-web r6assistant-web
    magic-web library-search-web magic-collection-web receipt-web library-web
    family-web adhd-web bartab-web auction-web market-web imagegen-web
    cloudoverblik-web testhub-web club-web musik-web magazine-web
    magazine-search-web musik-studio-web r6intel-web r6strat-web live-web
    image-processor scan-web upload-web dailybrief-web poll-web mediaseller-web
    uret-web stream-web)

for svc in "${ALL_MANAGED[@]}"; do
    is_excluded "$svc" && continue
    is_running "$svc" || continue

    host="${SERVICE_HOST[$svc]:-}"
    if [[ -n "$host" ]]; then
        seen=$(last_seen_epoch "$host")
        if [[ -z "$seen" ]]; then
            # Tracked app but no hits ever recorded — treat as idle.
            idle_for=$((IDLE_SECONDS + 1))
        else
            idle_for=$((now - seen))
        fi
    else
        started=$(container_started_epoch "$svc")
        idle_for=$((now - ${started:-now}))
    fi

    if (( idle_for > IDLE_SECONDS )); then
        stop_service "$svc"
        # magic-postgres is only useful while magic-web is up — stop it too.
        if [[ "$svc" == "magic-web" ]]; then
            stop_service "magic-postgres"
        fi
    fi
done
