#!/bin/bash
# Recupere les secrets dans SSM Parameter Store, tire l'image depuis ECR et
# remplace le conteneur de l'API.
#
# Les secrets sont stockes sous /teamchat/prod/<NOM_VARIABLE>, par exemple :
#   /teamchat/prod/ConnectionStrings__Default   (SecureString)
#   /teamchat/prod/Jwt__Secret                  (SecureString)
#   /teamchat/prod/Storage__Provider            = S3
#   /teamchat/prod/Storage__S3__Bucket          = teamchat-uploads-...
set -euo pipefail

DEPLOY_DIR=/opt/teamchat/deploy
ENV_FILE=/opt/teamchat/api.env
CONTAINER=teamchat-api
SSM_PATH=/teamchat/prod

# shellcheck source=/dev/null
source "$DEPLOY_DIR/image.env"   # IMAGE_URI, AWS_REGION

# 1. Variables d'environnement depuis SSM (le nom du parametre devient le nom de la variable).
umask 077
aws ssm get-parameters-by-path \
  --region "$AWS_REGION" --path "$SSM_PATH" --recursive --with-decryption \
  --query 'Parameters[*].[Name,Value]' --output text \
  | while IFS=$'\t' read -r name value; do
      echo "${name##*/}=${value}"
    done > "$ENV_FILE.tmp"
mv "$ENV_FILE.tmp" "$ENV_FILE"

# 2. Image depuis ECR.
REGISTRY="${IMAGE_URI%%/*}"
aws ecr get-login-password --region "$AWS_REGION" | docker login --username AWS --password-stdin "$REGISTRY"
docker pull "$IMAGE_URI"

# 3. Remplacement du conteneur (CloudFront -> port 80 de l'instance -> 8080 du conteneur).
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
  --log-opt awslogs-create-group=true \
  "$IMAGE_URI"

# 4. Menage des anciennes images.
docker image prune -af --filter "until=72h" || true
