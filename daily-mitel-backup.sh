#!/bin/bash
# Daily consistent copy of MinElpris (mitel.db): NAS ~/Backups/mitel-daily + photoserver ~/nas-backup/mitel-daily, 7 kept.
set -u
SRC=/volume1/docker/martinsuite/mitel/data/mitel.db
DIR=/volume1/homes/MartinHvidberg/Backups/mitel-daily
PS=martinhvidberg@10.0.0.200
D=$(date +%Y%m%d)
log() { echo "[$(date "+%F %T")] $*"; }
mkdir -p "$DIR"
sqlite3 "$SRC" ".backup $DIR/mitel-$D.db" || { log "FAIL sqlite backup"; exit 1; }
gzip -f "$DIR/mitel-$D.db"
ls -1t "$DIR"/mitel-*.db.gz | tail -n +8 | xargs -r rm -f
ssh -o BatchMode=yes -o ConnectTimeout=10 "$PS" "mkdir -p ~/nas-backup/mitel-daily && cat > ~/nas-backup/mitel-daily/mitel-$D.db.gz && ls -1t ~/nas-backup/mitel-daily/mitel-*.db.gz | tail -n +8 | xargs -r rm -f" < "$DIR/mitel-$D.db.gz" \
  && log "OK $(du -h "$DIR/mitel-$D.db.gz" | cut -f1) nas+photoserver" || log "WARN photoserver copy failed (NAS copy ok)"
