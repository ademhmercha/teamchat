#!/bin/bash
# Lit le resultat du scan ECR (scan a l'envoi) de l'image qui vient d'etre poussee et fait echouer le build
# si une vulnerabilite CRITICAL est trouvee.
#
# Usage : check-ecr-scan.sh <depot> <tag>
# Variables :
#   ECR_SCAN_FAIL_ON_CRITICAL  "true" (defaut) : echec du build ; "false" : affiche seulement les resultats
#   ECR_SCAN_IGNORED_CVES      CVE acceptees (separees par des virgules ou espaces), ex. "CVE-2024-1234,CVE-2025-9999"
#                              (faux positifs ou failles de l'image de base sans correctif disponible)
#
# Le scan ECR "basic" ne dit pas si un correctif existe : une CVE CRITICAL non corrigee dans l'image de base
# bloquera le build. Dans ce cas : l'examiner, puis l'ajouter a ECR_SCAN_IGNORED_CVES (ou desactiver le controle).
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
  # Echec du scanner lui-meme (limite, image non supportee) : on previent sans bloquer le deploiement.
  echo "ATTENTION : scan ECR indisponible ou en echec pour $REPOSITORY:$TAG, aucune verification effectuee."
  exit 0
fi

# Litteral JMESPath entre backticks, defini a part (le parseur de bash le supporte mal dans $(...)).
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
