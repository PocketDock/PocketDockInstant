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
fly deploy --ha=false
# If you didn't allocate a dedicated IPv4 address in the previous step, run
fly ips allocate-v4
# Run "fly machine ls" and note what region the machines are in. Needed for the next step.
```

Backend deployment instructions:
```sh
cd backend
fly app create # Note the name that was provided/generated
# Copy example.env to .env and fill it out
./deploy.sh

# If you get the below error, run the deploy script again.
#   "Error: machine failed to reach desired start state, and restart policy was set to no restart", 
```