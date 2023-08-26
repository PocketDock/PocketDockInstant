#!/bin/sh
mkdir /tmp/fb
cd /tmp/fb
/app/filebrowser config import -d /tmp/fb/db.db /app/config.json
/app/filebrowser users import -d /tmp/fb/db.db /app/users.json
/app/filebrowser -d /tmp/fb/db.db