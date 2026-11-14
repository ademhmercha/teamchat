# Valeurs NON secretes de l'environnement prod (committees).
# Secrets et valeurs personnelles : variables d'environnement TF_VAR_* ou fichier *.auto.tfvars local :
#   TF_VAR_ses_from_address       (obligatoire)
#   TF_VAR_admin_seed_email
#   TF_VAR_admin_seed_password
#   TF_VAR_google_client_id
environment = "prod"
aws_region  = "eu-west-1"

db_instance_class      = "db.t4g.micro"
db_deletion_protection = true
db_skip_final_snapshot = false

instance_type = "t3.micro"
