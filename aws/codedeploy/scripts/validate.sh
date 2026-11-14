#!/bin/bash
# Attend que l'API reponde (migrations EF Core comprises) ; sinon le deploiement echoue.
set -uo pipefail
for i in $(seq 1 30); do
  if curl -fsS http://localhost/api/health > /dev/null; then
    echo "API OK"
    exit 0
  fi
  sleep 3
done
echo "L'API ne repond pas apres 90 s :"
docker logs --tail 100 teamchat-api || true
exit 1
