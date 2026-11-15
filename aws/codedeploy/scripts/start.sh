#!/bin/bash
set -euo pipefail

DEPLOY_DIR=/opt/teamchat/deploy
ENV_FILE=/opt/teamchat/api.env
CONTAINER=teamchat-api
SSM_PATH=/teamchat/prod

source "$DEPLOY_DIR/image.env"

umask 077
aws ssm get-parameters-by-path \
  --region "$AWS_REGION" --path "$SSM_PATH" --recursive --with-decryption \
  --query 'Parameters[*].[Name,Value]' --output text \
  | while IFS=$'\t' read -r name value; do
      echo "${name##*/}=${value}"
    done > "$ENV_FILE.tmp"
mv "$ENV_FILE.tmp" "$ENV_FILE"

REGISTRY="${IMAGE_URI%%/*}"
aws ecr get-login-password --region "$AWS_REGION" | docker login --username AWS --password-stdin "$REGISTRY"
docker pull "$IMAGE_URI"

docker rm -f "$CONTAINER" 2>/dev/null || true
docker run -d \
  --name "$CONTAINER" \
  --restart unless-stopped \
  -p 80:8080 \
  --env-file "$ENV_FILE" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e AWS_REGION="$AWS_REGION" \
  --log-driver awslogs \
  --log-opt awslogs-region="$AWS_REGION" \
  --log-opt awslogs-group=/teamchat/api \
  "$IMAGE_URI"

docker image prune -af --filter "until=72h" || true
