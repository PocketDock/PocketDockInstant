#!/bin/bash -e

source .env

fly secrets set --app $BACKEND_APP_NAME --stage "API_BASE_URL=$API_BASE_URL" "REMOTE_SERVER_PASS=$REMOTE_SERVER_PASS"

isPrivateIpCreated=$(fly ip list --app "$BACKEND_APP_NAME" --json | jq '[.[] | select(.Type == "private_v6")] | length')

if [ "$isPrivateIpCreated" == "0" ]; then
    fly ips allocate-v6 --private --app $BACKEND_APP_NAME
fi

hasImages=$(fly image show --app $BACKEND_APP_NAME --json)

if [ "$hasImages" == "null" ]; then
    fly machine run busybox --app $BACKEND_APP_NAME -- sleep 5
    sleep 1
    tempMachineId=$(fly machine list --app $BACKEND_APP_NAME --json | jq -r '.[] | select (.image_ref.repository == "library/busybox").id')
fi

imageTag=$(date +%s)

fly deploy --image-label $imageTag --build-only --push --app $BACKEND_APP_NAME

if [ ! -z "$tempMachineId" ]; then
    fly machine rm --app $BACKEND_APP_NAME -f $tempMachineId
fi


machineList=$(fly machine list --app $BACKEND_APP_NAME --json | jq '[.[] | select(.config.metadata?.DEPLOY_SCRIPT_MANAGED == "true")]')
if [[ "$machineList" != [* ]]; then
    machineList="[]"
fi
startingPort=$(echo $machineList | jq '[.[].config.services[]?.ports[].port] | max + 100')
machineCount=$(echo $machineList | jq '. | length')
existingMachineIds=$(echo $machineList | jq -r '.[].id' | tr -d ' ' | tr '\r' '\n')

machineCountToLaunch=$((DESIRED_MACHINE_COUNT - machineCount))

for ((i=1; i<=machineCountToLaunch; i++))
do
    echo "===================="
    newPort=$(( (100*i) + startingPort ))
    fly machine run --app $BACKEND_APP_NAME \
        --port $newPort':7000/tcp:http' \
        --region $APP_REGION \
        --autostop=false \
        --vm-memory 1024 \
        --restart no \
        --metadata DEPLOY_SCRIPT_MANAGED=true \
        --detach \
        "registry.fly.io/$BACKEND_APP_NAME:$imageTag"
done

if [ "$machineCount" -gt 0 ]; then
    echo -n $existingMachineIds | xargs -d ' ' -I {} fly machine update {} -y --app $BACKEND_APP_NAME \
            --autostop=false \
            --vm-memory 1024 \
            --restart no \
            --metadata DEPLOY_SCRIPT_MANAGED=true \
            --detach \
            --skip-health-checks \
            --image "registry.fly.io/$BACKEND_APP_NAME:$imageTag"
fi
if [ "$machineCountToLaunch" -lt 0 ]; then
    echo -n $existingMachineIds | tr ' ' '\n' | tail -n $machineCountToLaunch | xargs -I {} fly machine rm --app $BACKEND_APP_NAME -f {}
fi

newMachineList=$(fly machine list --app $BACKEND_APP_NAME --json | jq '[.[] | select(.config.metadata?.DEPLOY_SCRIPT_MANAGED == "true")]')

privateIp=$(fly ips list --app $BACKEND_APP_NAME --json | jq -r '.[] | select(.Type == "private_v6") | .Address')

echo $newMachineList > upload.tmp

curl --insecure "$API_BASE_URL/api/Server/UpdateServerList?remoteServerPass=$REMOTE_SERVER_PASS" \
--form "TriggerIp=$privateIp" \
--form "Domain=$FRONTEND_DOMAIN" \
--form "Machines=@upload.tmp"

#rm upload.tmp