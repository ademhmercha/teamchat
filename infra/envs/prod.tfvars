# Valeurs NON secretes de l'environnement prod (committees).
# Secrets et valeurs personnelles : variables d'environnement TF_VAR_* ou fichier *.auto.tfvars local :
#   TF_VAR_ses_from_address       (obligatoire)
#   TF_VAR_admin_seed_email
#   TF_VAR_admin_seed_password
#   TF_VAR_google_client_id
environment = "prod"
aws_region  = "eu-north-1"

db_instance_class = "db.t4g.micro"
# 7 apres passage au plan payant (le Free plan refuse une retention plus longue)
rds_backup_retention_days = 1
db_deletion_protection    = true
db_skip_final_snapshot    = false

instance_type = "t3.micro"

# Verification des pull requests par CodeBuild (connexion GitHub validee dans la console).
enable_pr_checks = true

# Budget AWS existant (cree a la main) : importe au premier apply, puis repasser import_existing_budget a false.
# Le nom doit etre IDENTIQUE a celui de la console AWS Budgets (espaces et fautes comprises).
budget_name            = "team chat monthy budget"
budget_limit_usd       = "30"
import_existing_budget = true
