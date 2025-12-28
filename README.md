# CO2Bot

A Telegram bot for retrieving data from QingPing thermometers.

> Also, my playground for learning F# :)

# Quickstart

1. Configure your thermometer to push data to QingPing using QingPing IoT app.
2. Get your App Key and App Secret from https://developer.qingping.co/personal/permissionApply
3. Find your device's MAC/SN address from https://developer.qingping.co/private/device-binding
   1. Click on "Add Device"
   2. Choose your device type
   3. Copy `MAC/SN` from the table
4. Look at `docker-compose/config/config.example.yaml` to see available options
5. Create `docker-compose/config/config.yaml` with your App Key, App Secret and device MAC
6. ```bash
   cd docker-compose
   docker compose up -d
   docker compose logs -f
   ```