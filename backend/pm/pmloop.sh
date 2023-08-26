#!/bin/sh
trap "" INT TSTP

while [ ! -f /mgmt/stop_server ]
do
	bin/php7/bin/php PocketMine-MP.phar --no-wizard --enable-ansi --data=/data --plugins=/data/plugins --server-port=$1 --server-portv6=$1

    [ ! -f /mgmt/stop_server ] && echo -en "================Restarting server in 5 seconds================\n\n"
	sleep 5
done
