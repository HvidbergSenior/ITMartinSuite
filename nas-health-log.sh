#!/bin/bash
# Appends a one-line NAS health snapshot every run. Check anytime with:
#   tail -50 /volume1/homes/MartinHvidberg/nas-health.log
#
# Install (run once on the NAS, alongside the idle-stop cron job):
#   crontab -e
#   0 * * * * /volume1/homes/MartinHvidberg/martinsuite-magic/nas-health-log.sh >> /volume1/homes/MartinHvidberg/nas-health.log 2>&1

load=$(cat /proc/loadavg | awk '{print $1, $2, $3}')
mem=$(free -h | awk '/^Mem:/ {print "used="$3" free="$4" avail="$7}')
swap=$(free -h | awk '/^Swap:/ {print "used="$3" free="$4}')

echo "[$(date -u '+%Y-%m-%d %H:%M:%S UTC')] load(1/5/15)=$load mem($mem) swap($swap)"
