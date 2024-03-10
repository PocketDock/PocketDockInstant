#!/bin/bash -eu

source .env

# Use jq in binary mode so cygwin doesn't add \r to the output
function jq() {
    $(which jq) -b "$@"
}

# Get the current apps
primaryAppName="${PROXY_APP_PREFIX}-proxy-${PRIMARY_REGION}"
if [ $(fly apps list -o $ORG --json | jq "[.[] | select(.Name == \"${primaryAppName}\")] | length") -eq 0 ]; then
    fly apps create -o "$ORG" "$primaryAppName"
fi

cat .env | tr -d '\r' | fly secrets import --app "$primaryAppName"
fly deploy --ha=false -c fly.toml --app "$primaryAppName" --no-public-ips --region "$PRIMARY_REGION"

imageName=$(fly machines list -a $primaryAppName --json | jq -r '.[].config.image')
if [ -z "$imageName" ]; then
    echo "No image found for $primaryAppName"
    exit 1
fi

currentApps=$(fly apps list -o $ORG --json | jq -r ".[] | .Name | select(. | startswith(\"${PROXY_APP_PREFIX}-proxy\"))")

newAppNames=()

for region in "${REGIONS[@]}"
do
    appName="${PROXY_APP_PREFIX}-proxy-${region}"
    newAppNames+=("$appName")
    if [[ $currentApps =~ $region ]]; then
        echo "App $appName already exists"
    else
        fly apps create -o "$ORG" "$appName"
    fi
    fly deploy --ha=false -c fly.toml --app "$appName" --no-public-ips --image "$imageName" --region "$region"
    cat .env | tr -d '\r' | fly secrets import --app "$appName"
done

# Reload app list
currentApps=$(fly apps list -o $ORG --json | jq -r ".[] | .Name | select(. | startswith(\"${PROXY_APP_PREFIX}-proxy\"))")

# Loop through the current apps and delete if they don't exist in the regions list
for appToCheck in $currentApps
do
    if [[ ! " ${newAppNames[@]} " =~ " ${appToCheck} " ]]; then
        echo fly apps destroy "$appToCheck"
    fi
done

set -x
echo "Updating proxy list"
curl -i -X POST --insecure "$API_BASE_URL/api/Server/UpdateProxyList?remoteServerPass=$REMOTE_SERVER_PASS" \