#!/bin/bash
# Usage: bash gd-batch-pull.sh <batchname> <start> <end>
# Example: bash gd-batch-pull.sh batch2 011 020
BATCHNAME="$1"
START="$2"
END="$3"

if [ -z "$BATCHNAME" ] || [ -z "$START" ] || [ -z "$END" ]; then
  echo "Usage: bash gd-batch-pull.sh <batchname> <start> <end>"
  exit 1
fi

mkdir -p "C:/FileSorterTests/DropboxSource/gd-$BATCHNAME-zips" "C:/FileSorterTests/DropboxSource/mie-googledrive-$BATCHNAME"

for n in $(seq -w "$START" "$END"); do
  scp -O "martinhvidberg@10.0.0.126:/volume1/docker/filesorter/library/mie_googledrive_2026-07-11/takeout-20260711T082224Z-4-$n.zip" "C:/FileSorterTests/DropboxSource/gd-$BATCHNAME-zips/"
done

cd "C:/FileSorterTests/DropboxSource/gd-$BATCHNAME-zips" || exit 1
for f in *.zip; do
  unzip -q "$f" -d "C:/FileSorterTests/DropboxSource/mie-googledrive-$BATCHNAME"
  rm -f "$f"
done

echo "Batch $BATCHNAME ready at /data/mie-googledrive-$BATCHNAME"
