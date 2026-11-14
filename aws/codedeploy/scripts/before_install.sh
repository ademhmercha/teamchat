#!/bin/bash
# Nettoie l'ancienne revision copiee (le conteneur continue de tourner).
set -euo pipefail
mkdir -p /opt/teamchat
rm -rf /opt/teamchat/deploy
