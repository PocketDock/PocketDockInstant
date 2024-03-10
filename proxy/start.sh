#!/bin/sh -x
CONFIG_URL="${API_BASE_URL}/api/Server/TraefikProxyConfig?region=${FLY_REGION}\&remoteServerPass=${REMOTE_SERVER_PASS}"
FILE=/app/access.log
serverName=$(hostname)
OLDTIME=300

sed -i 's|{{CONFIG_URL}}|'"$CONFIG_URL"'|' /app/traefik.yml
traefik --configFile=/app/traefik.yml &
touch $FILE
sleep 2
set +x

# Stop proxy after no pings for 5 minutes
while true
do
    CURTIME=$(date +%s)
    FILETIME=$(stat $FILE -c %Y)
    TIMEDIFF=$(expr $CURTIME - $FILETIME)

    # Check if file older
    if [ $TIMEDIFF -gt $OLDTIME ]; then
        set -x
        echo "killing"
        killall -9 traefik
        sleep 1
        killall -9 traefik | true
        echo hi
        curl -X POST "${API_BASE_URL}/api/Server/DeallocateIp?proxyId=${serverName}&remoteServerPass=${REMOTE_SERVER_PASS}"
        exit 0
    fi
    sleep 1
done