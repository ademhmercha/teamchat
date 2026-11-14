# Parametres lus par aws/codedeploy/scripts/start.sh : le nom du parametre (apres le dernier "/")
# devient le nom de la variable d'environnement du conteneur.
#
# Les valeurs sensibles se retrouvent aussi dans le state Terraform (bucket chiffre, prive).

resource "random_password" "jwt" {
  length  = 64
  special = false
}

locals {
  cloudfront_url = "https://${aws_cloudfront_distribution.main.domain_name}"

  # Parametres en clair (String).
  ssm_plain = {
    Jwt__Issuer                 = "teamchat-api"
    Jwt__Audience               = "teamchat-app"
    Cors__AllowedOrigins        = local.cloudfront_url
    App__PublicUrl              = local.cloudfront_url
    Storage__Provider           = "S3"
    Storage__S3__Bucket         = aws_s3_bucket.uploads.bucket
    Storage__S3__Region         = var.aws_region
    Storage__S3__ForcePathStyle = "false"
    Email__Provider             = "Ses"
    Email__FromAddress          = var.ses_from_address
    Email__Region               = var.aws_region
  }

  # Secrets (SecureString, cle aws/ssm).
  ssm_secure = {
    # "Trust Server Certificate" : le trafic est chiffre, mais l'autorite RDS n'est pas dans le magasin
    # de certificats de l'image aspnet ; sans cela la validation echoue. Acces limite au groupe de securite.
    ConnectionStrings__Default = "Host=${aws_db_instance.main.address};Port=${aws_db_instance.main.port};Database=${var.db_name};Username=${var.db_username};Password=${random_password.db.result};SSL Mode=Require;Trust Server Certificate=true"
    Jwt__Secret                = random_password.jwt.result
  }
}

resource "aws_ssm_parameter" "plain" {
  for_each = local.ssm_plain

  name  = "${local.ssm_path}/${each.key}"
  type  = "String"
  value = each.value
}

resource "aws_ssm_parameter" "secure" {
  for_each = nonsensitive(toset(keys(local.ssm_secure)))

  name  = "${local.ssm_path}/${each.key}"
  type  = "SecureString"
  value = local.ssm_secure[each.key]
}

# Valeurs fournies par toi : creees seulement si renseignees (SSM refuse une valeur vide).
resource "aws_ssm_parameter" "admin_seed_email" {
  count = var.admin_seed_email != "" ? 1 : 0

  name  = "${local.ssm_path}/Admin__SeedEmail"
  type  = "String"
  value = var.admin_seed_email
}

resource "aws_ssm_parameter" "admin_seed_password" {
  count = nonsensitive(var.admin_seed_password != "") ? 1 : 0

  name  = "${local.ssm_path}/Admin__SeedPassword"
  type  = "SecureString"
  value = var.admin_seed_password
}

resource "aws_ssm_parameter" "google_client_id" {
  count = nonsensitive(var.google_client_id != "") ? 1 : 0

  name  = "${local.ssm_path}/Google__ClientId"
  type  = "SecureString"
  value = var.google_client_id
}
