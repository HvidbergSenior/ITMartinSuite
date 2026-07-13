#!/bin/bash
# Estimate how long until a customer hits their next cloud storage limit,
# based on their own real recent upload rate — not a guess.
#
# Works two ways:
#  1. On an already-organized FileSorter library (Images/Videos/YYYY/MM-Month)
#  2. On a RAW, unsorted folder — e.g. a quick partial download straight from
#     a customer's phone/iCloud during the first free visit, before any paid
#     work has happened. Buckets files by their own file date directly.
#
# Usage: bash growth-estimate.sh <folder-path> <free-space-GB> [months-back]
# Example: bash growth-estimate.sh /volume1/docker/filesorter/library/mie 30 12
# Example (raw, on-site): bash growth-estimate.sh /data/quick-sample 5 6

export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL='*'

LIBPATH="$1"
FREE_GB="$2"
MONTHS_BACK="${3:-12}"

if [ -z "$LIBPATH" ] || [ -z "$FREE_GB" ]; then
  echo "Usage: bash growth-estimate.sh <folder-path> <free-space-GB> [months-back, default 12]"
  exit 1
fi

cd "$LIBPATH" || { echo "Path not found: $LIBPATH"; exit 1; }

if [ -d "Images" ] || [ -d "Videos" ]; then
  # Mode 1: already-organized FileSorter library — trust the Year/Month folder names
  find Images Videos -mindepth 2 -maxdepth 2 -type d 2>/dev/null | sort | while read -r dir; do
    size=$(du -sb "$dir" 2>/dev/null | cut -f1)
    key=$(echo "$dir" | awk -F/ '{print $2 "/" $3}')
    echo "$key $size"
  done > /tmp/growth_raw.txt
else
  # Mode 2: raw, unsorted folder (e.g. a fresh download/extract on-site).
  # File modification dates are NOT reliable here — they reflect when the
  # file was downloaded/extracted, not when the photo/video was taken. Read
  # the real capture date from EXIF instead, via a throwaway exiftool container.
  echo "(Rå mappe uden Images/Videos-struktur — læser EXIF-optagelsesdato, ikke fil-dato)"
  docker run --rm -v "${LIBPATH}:/data" alpine sh -c \
    "apk add -q exiftool 2>/dev/null; exiftool -T -r -DateTimeOriginal -CreateDate -FileModifyDate -FileSize# /data 2>/dev/null" \
    | awk -F'\t' '{
        date = ($1 != "-" && $1 != "") ? $1 : (($2 != "-" && $2 != "") ? $2 : $3);
        size = $4;
        if (date != "" && size ~ /^[0-9]+$/) {
          split(date, dp, ":");
          key = dp[1] "/" dp[2];
          print key, size
        }
      }' > /tmp/growth_raw.txt
fi

# Collapse to one total per Year/Month, keep chronological order
awk '{
  key = $1;
  sum[key] += $2;
  if (!(key in seen)) { order[++n] = key; seen[key] = 1 }
}
END {
  for (i = 1; i <= n; i++) print order[i], sum[order[i]]
}' /tmp/growth_raw.txt | sort > /tmp/growth_by_month.txt

TOTAL_MONTHS=$(wc -l < /tmp/growth_by_month.txt)
USE_MONTHS=$MONTHS_BACK
if [ "$TOTAL_MONTHS" -lt "$MONTHS_BACK" ]; then
  USE_MONTHS=$TOTAL_MONTHS
fi

echo "=== Seneste $USE_MONTHS måneder ==="
tail -n "$USE_MONTHS" /tmp/growth_by_month.txt | awk '{
  gb = $2 / 1073741824;
  printf "%-20s %.2f GB\n", $1, gb
}'

AVG_BYTES=$(tail -n "$USE_MONTHS" /tmp/growth_by_month.txt | awk '{sum+=$2; n++} END {if(n>0) print sum/n; else print 0}')
AVG_GB=$(awk -v b="$AVG_BYTES" 'BEGIN{printf "%.3f", b/1073741824}')

echo ""
echo "=== Resultat ==="
echo "Gennemsnitlig vækst: $AVG_GB GB/måned (baseret på $USE_MONTHS måneder)"

MONTHS_LEFT=$(awk -v free="$FREE_GB" -v avg="$AVG_GB" 'BEGIN{ if (avg > 0) printf "%.0f", free/avg; else print "uendelig (ingen vækst målt)" }')
echo "Med $FREE_GB GB ledig plads: ca. $MONTHS_LEFT måneder til næste loft nås"

YEARS=$(awk -v m="$MONTHS_LEFT" 'BEGIN{ if (m ~ /^[0-9]+$/) printf "%.1f", m/12 }')
if [ -n "$YEARS" ]; then
  echo "(ca. $YEARS år)"
fi
