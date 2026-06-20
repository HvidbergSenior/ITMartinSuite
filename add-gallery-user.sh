#!/bin/bash
# Add a personal gallery for a new person.
# Usage: ./add-gallery-user.sh <name>
# Example: ./add-gallery-user.sh emma
#
# - Creates /volume1/docker/filesorter/library/<name>/
# - Starts a gallery container pointing at that folder
# - Prints the Cloudflare hostname to add

set -e

NAME="${1,,}"  # lowercase

if [ -z "$NAME" ]; then
  echo "Usage: ./add-gallery-user.sh <name>"
  exit 1
fi

FOLDER="/volume1/docker/filesorter/library/$NAME"
CONTAINER="gallery-$NAME"
ENV_FILE="/volume1/homes/MartinHvidberg/martinsuite-magic/magic.env"
IMAGE="martinsuite-gallery-web"
NETWORK="martinsuite_martinnet"

# Check container doesn't already exist
if docker ps -a --format '{{.Names}}' | grep -q "^$CONTAINER$"; then
  echo "Container $CONTAINER already exists. Remove it first with:"
  echo "  docker rm -f $CONTAINER"
  exit 1
fi

# Find next free port (starting after 8091)
PORT=8092
while docker ps -a --format '{{.Ports}}' | grep -q ":$PORT->"; do
  PORT=$((PORT + 1))
done

# Create the folder
mkdir -p "$FOLDER"
echo "✓ Created $FOLDER"

# Start the container (reuses existing built image — no rebuild)
docker run -d \
  --name "$CONTAINER" \
  --network "$NETWORK" \
  --restart unless-stopped \
  -p "$PORT:8080" \
  --env-file "$ENV_FILE" \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e "MediaSettings__LibraryRoot=/library" \
  -e "ConnectionStrings__MediaDb=Data Source=/library/.media.db" \
  -v "$FOLDER:/library:ro" \
  "$IMAGE"

echo "✓ Container $CONTAINER started on port $PORT"
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo " Add in Cloudflare dashboard:"
echo "   Subdomain : $NAME"
echo "   Domain    : itmartin.dk"
echo "   Service   : http://$CONTAINER:8080"
echo ""
echo " URL: https://$NAME.itmartin.dk"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo " Drop files for $NAME into:"
echo "   $FOLDER"
