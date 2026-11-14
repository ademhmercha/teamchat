# State distant dans S3, verrouillage par fichier (use_lockfile) : pas de table DynamoDB.
# Le nom du bucket contient l'identifiant du compte : il n'est pas ecrit ici, on le passe a l'init :
#
#   terraform init \
#     -backend-config=envs/prod.backend.hcl \
#     -backend-config="bucket=teamchat-tfstate-<ACCOUNT_ID>"
#
# (la cle teamchat/<env>/terraform.tfstate est dans envs/<env>.backend.hcl)
terraform {
  backend "s3" {}
}
