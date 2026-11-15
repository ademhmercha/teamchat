#!/bin/bash
set -uo pipefail

REPOSITORY="${1:?depot ECR manquant}"
TAG="${2:?tag image manquant}"
FAIL_ON_CRITICAL="${ECR_SCAN_FAIL_ON_CRITICAL:-true}"
IGNORED="${ECR_SCAN_IGNORED_CVES:-}"

if [ "$FAIL_ON_CRITICAL" != "true" ]; then
  echo "Controle du scan ECR desactive (ECR_SCAN_FAIL_ON_CRITICAL=$FAIL_ON_CRITICAL)."
  exit 0
fi

echo "Attente du scan ECR de $REPOSITORY:$TAG ..."
if ! aws ecr wait image-scan-complete --repository-name "$REPOSITORY" --image-id "imageTag=$TAG"; then
  echo "ATTENTION : scan ECR indisponible ou en echec pour $REPOSITORY:$TAG, aucune verification effectuee."
  exit 0
fi

QUERY='imageScanFindings.findings[?severity==`CRITICAL`].name'
CRITICAL=$(aws ecr describe-image-scan-findings \
  --repository-name "$REPOSITORY" --image-id "imageTag=$TAG" \
  --query "$QUERY" --output text) || {
  echo "ATTENTION : lecture des resultats du scan impossible, aucune verification effectuee."
  exit 0
}

BLOCKING=()
for cve in $CRITICAL; do
  [ "$cve" = "None" ] && continue
  if [[ ",${IGNORED// /,}," == *",$cve,"* ]]; then
    echo "CVE critique ignoree (ECR_SCAN_IGNORED_CVES) : $cve"
  else
    BLOCKING+=("$cve")
  fi
done

if [ "${#BLOCKING[@]}" -gt 0 ]; then
  echo "ECHEC : ${#BLOCKING[@]} vulnerabilite(s) CRITICAL dans $REPOSITORY:$TAG :"
  printf '  - %s\n' "${BLOCKING[@]}"
  echo "Details : console ECR > $REPOSITORY > $TAG > Vulnerabilites. Pour passer outre : ECR_SCAN_IGNORED_CVES ou ECR_SCAN_FAIL_ON_CRITICAL=false."
  exit 1
fi

echo "Scan ECR : aucune vulnerabilite CRITICAL bloquante."
