#!/bin/bash -e

source .env

cat .env | tr -d '\r' | fly secrets import

fly deploy --smoke-checks=false --strategy=immediate --ha=false

fly scale count --region $PRIMARY_REGION $PRIMARY_REGION_INSTANCES

fly machine list --json | jq -r '.[].id' | tr -d ' ' | tr '\r' '\n' | xargs -I {} fly machine update {} -y --autostop=false --restart no --detach --skip-health-checks

fly machine list --json > server_upload.tmp

curl --insecure "$API_BASE_URL/api/Server/UpdateServerList?remoteServerPass=$REMOTE_SERVER_PASS" \
--form "Machines=@server_upload.tmp" --form "CountPerRegion=${PRIMARY_REGION_INSTANCES}"

rm server_upload.tmp