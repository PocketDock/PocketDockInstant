#!/bin/sh -ex
set -ex
exec > >(tee -a "/tmp/logdata.log") 2>&1
#TODO:FUTURE: Custom apparmor profile based off of the default docker one
#   Dump default docker apparmor profile: https://github.com/moby/moby/issues/33060#issuecomment-490064111
#   (Linux LSMs like apparmor are not currently available in fly.io right now)

echo 1 > /proc/sys/net/ipv4/ip_forward

# PMPORT=30000
# LOCAL_SERVER_PASS=testpass

serverName=$(hostname)
curl --fail-with-body "${API_BASE_URL}/api/Server/Settings?serverId=${serverName}&remoteServerPass=${REMOTE_SERVER_PASS}" > settings.env
set -a
. settings.env
set +a
echo "=========File:========"
cat settings.env

export HOSTNAME

mkdir /mgmt
chmod -R +r /mgmt

chown -R 2000:2000 /data

#docker-proxy writes logs to file descriptor 3, so we open it here and redirect it to stdout
exec 3>&1
cd /pm
echo PMPORT=$PMPORT > /pm/rootfs/.env

if [ -n "${PM_VERSION}" ]; then
    ln -s /pm/rootfs/app /app
    cd /app
    mv start.sh start.sh.new
    /pm_install.sh -r -v "${PM_VERSION}"
    mv start.sh.new start.sh
    cd /pm
    rm /app
fi
runc run pm --pid-file /pm_container.pid &
sleep 1
nsenter -t $(cat /pm_container.pid) -n /iptables_setup.sh

sed -i "s/LOCAL_SERVER_PASS/${LOCAL_SERVER_PASS}/" /authn.yaml

basic-auth-reverse-proxy serve --port 7000 --upstream http://$(cat /pm/.ip):7000 --auth-config /authn.yaml &
#docker-proxy -proto tcp -host-ip 0.0.0.0 -host-port 7000 -container-ip $(cat /pm/.ip) -container-port 7000 &

docker-proxy -proto udp -host-ip [::] -host-port $PMPORT -container-ip $(cat /pm/.ip) -container-port $PMPORT &

cd /filebrowser
runc run filebrowser &
sleep 1

basic-auth-reverse-proxy serve --port 8000 --upstream http://$(cat /filebrowser/.ip):8000 --auth-config /authn.yaml &

#sleep infinity

echo "Starting monitor"
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true McQuery
set +e
echo "Monitor stopped"
touch /mgmt/stop_server
chmod +r /mgmt/stop_server
# This is killed separately so we have a nice message when PM stops
kill $(ps h --ppid $(pgrep pmloop) -o pid)
sleep 2
runc kill -a pm KILL
runc kill -a filebrowser KILL
echo "Killed servers"
curl -X POST "${API_BASE_URL}/api/Server/RemoveServer?serverId=${serverName}&remoteServerPass=${REMOTE_SERVER_PASS}"
echo "Sent RemoveServer"
