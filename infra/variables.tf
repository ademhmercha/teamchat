variable "aws_region" {
  type    = string
  default = "eu-north-1"
}

variable "environment" {
  description = "Nom de l'environnement (prod). Sert aux tags, au chemin SSM /teamchat/<env>/ et aux noms."
  type        = string
  default     = "prod"
}

# ---- Base de donnees ----

variable "db_name" {
  type    = string
  default = "teamchat"
}

variable "db_username" {
  type    = string
  default = "teamchat_admin"
}

variable "db_instance_class" {
  type    = string
  default = "db.t4g.micro"
}

variable "rds_backup_retention_days" {
  description = "Retention des sauvegardes automatiques RDS (jours). Free plan : 1 ; passer a 7 apres le passage au plan payant."
  type        = number
  default     = 1

  validation {
    condition     = var.rds_backup_retention_days >= 0 && var.rds_backup_retention_days <= 35
    error_message = "rds_backup_retention_days doit etre compris entre 0 et 35."
  }
}

variable "db_deletion_protection" {
  description = "Empeche la suppression de la base (passer a false avant un terraform destroy)."
  type        = bool
  default     = true
}

variable "db_skip_final_snapshot" {
  description = "true = pas de sauvegarde finale a la suppression (a eviter en prod)."
  type        = bool
  default     = false
}

# ---- Serveur ----

variable "instance_type" {
  type    = string
  default = "t3.micro"
}

# ---- Ressources us-east-1 (optionnelles, voir us_east_1.tf) ----

variable "enable_us_east_1_resources" {
  description = "Cree les ressources imposees en us-east-1 (certificat ACM, alarme CloudFront). Desactive : le compte peut bloquer cette region."
  type        = bool
  default     = false
}

variable "custom_domain" {
  description = "Domaine personnalise de l'application (ex. chat.exemple.com) : cree un certificat ACM si enable_us_east_1_resources = true."
  type        = string
  default     = ""
}

variable "alert_email" {
  description = "Adresse qui recoit l'alarme CloudFront 5xx (obligatoire si enable_us_east_1_resources = true)."
  type        = string
  default     = ""
}

# ---- Emails (SES) ----

variable "ses_from_address" {
  description = "Adresse d'expedition verifiee dans SES (Email__FromAddress). Une verification par email est envoyee a la creation."
  type        = string

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.ses_from_address)) && !endswith(var.ses_from_address, "example.com")
    error_message = "Renseigne une vraie adresse email (pas @example.com), par exemple via TF_VAR_ses_from_address."
  }
}

# ---- Secrets fournis par toi : jamais dans un tfvars committe ----
# Passe-les par l'environnement (TF_VAR_admin_seed_password=...) ou un fichier *.auto.tfvars
# local (ignore par git). Laisse vide pour ne pas creer le parametre SSM correspondant.

variable "admin_seed_email" {
  description = "Admin__SeedEmail : email du compte SuperAdmin cree au premier demarrage."
  type        = string
  default     = ""
}

variable "admin_seed_password" {
  description = "Admin__SeedPassword : mot de passe du compte SuperAdmin initial."
  type        = string
  default     = ""
  sensitive   = true
}

variable "google_client_id" {
  description = "Google__ClientId (connexion Google). Vide = connexion Google desactivee."
  type        = string
  default     = ""
  sensitive   = true
}
