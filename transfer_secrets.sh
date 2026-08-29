#!/bin/bash
cd "$(dirname "$0")"

projects=(ITMartin.Api ITMartin.Magic.Server ITMartinFileSorter.Server ITMartinFileSorter.Worker ITMartinImageProcessor.Worker ITMartinMusikStudio.Server ITMartinR6Assistant.Server ITMartinStarRealms.Server ITMartinTransit.Server ITMartinVlog.Server ITMartin.Receipt.Server)
files=(appsettings.Development.json appsettings.json appsettings.NAS.json)

for p in "${projects[@]}"; do
  for f in "${files[@]}"; do
    if [ -f "$p/$f" ]; then
      ssh Juliushvidberg@10.0.0.176 "cmd /c mkdir C:\\Users\\hvidb\\RiderProjects\\ITMartinSuite\\$p" 2>/dev/null
      scp "$p/$f" "Juliushvidberg@10.0.0.176:C:/Users/hvidb/RiderProjects/ITMartinSuite/$p/$f" && echo "OK: $p/$f"
    fi
  done
done

for p in ITMartinFileSorter.Server ITMartinFileSorter.Worker; do
  if [ -d "$p/ffmpeg" ]; then
    scp -r "$p/ffmpeg" "Juliushvidberg@10.0.0.176:C:/Users/hvidb/RiderProjects/ITMartinSuite/$p/ffmpeg" && echo "OK: $p/ffmpeg"
  fi
done

echo "DONE"
