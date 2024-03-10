## PocketDock

Pre-requisites:
- [Fly.io](https://fly.io) account
- [flyctl](https://fly.io/docs/hands-on/install-flyctl/) CLI
- Bash (WSL or Git Bash will work if on Windows)
- [jq](https://jqlang.github.io/jq/)
- Postgres database

Frontend deployment instructions:
```sh
cd frontend
# Copy example.env to .env and fill it out
fly app create # Note the name that was provided/generated
# Copy fly.toml.example and fill out app name
cat .env | fly secrets import
# Use --ha=false since we don't want UDP to be load balanced
# If asked, use a shared IP and not a dedicated IP
fly deploy --ha=false
```

Proxy deployment instructions:
```sh
cd proxy
# Copy example.env to .env and fill it out
./deploy.sh
```

Backend deployment instructions:
```sh
cd backend
fly app create # Note the name that was provided/generated
# Copy fly.toml.example and fill out app name
# Copy example.env to .env and fill it out
./update.sh
```