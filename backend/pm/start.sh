#!/bin/sh
. /.env
cd /app
screen -dmS pm ./pmloop.sh $PMPORT
gotty -p 7000 -w --reconnect ./console.sh