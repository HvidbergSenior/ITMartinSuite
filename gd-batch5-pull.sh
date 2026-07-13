#!/bin/bash
# The leftover parts that don't fit the sequential range pattern: 041, 042, 3-001
BATCHNAME=batch5

mkdir -p "C:/FileSorterTests/DropboxSource/gd-$BATCHNAME-zips" "C:/FileSorterTests/DropboxSource/mie-googledrive-$BATCHNAME"

scp -O "martinhvidberg@10.0.0.126:/volume1/docker/filesorter/library/mie_googledrive_2026-07-11/takeout-20260711T082224Z-4-041.zip" "C:/FileSorterTests/DropboxSource/gd-$BATCHNAME-zips/"
scp -O "martinhvidberg@10.0.0.126:/volume1/docker/filesorter/library/mie_googledrive_2026-07-11/takeout-20260711T082224Z-4-042.zip" "C:/FileSorterTests/DropboxSource/gd-$BATCHNAME-zips/"
scp -O "martinhvidberg@10.0.0.126:/volume1/docker/filesorter/library/mie_googledrive_2026-07-11/takeout-20260711T082224Z-3-001.zip" "C:/FileSorterTests/DropboxSource/gd-$BATCHNAME-zips/"

cd "C:/FileSorterTests/DropboxSource/gd-$BATCHNAME-zips" || exit 1
for f in *.zip; do
  unzip -q "$f" -d "C:/FileSorterTests/DropboxSource/mie-googledrive-$BATCHNAME"
  rm -f "$f"
done

echo "Batch $BATCHNAME ready at /data/mie-googledrive-$BATCHNAME"
