resource "random_password" "db" {
  length  = 32
  special = false # evite les caracteres interdits par RDS et les ';' de la chaine Npgsql
}

resource "aws_db_subnet_group" "main" {
  name       = "teamchat-${local.env}"
  subnet_ids = data.aws_subnets.default.ids
}

resource "aws_db_instance" "main" {
  identifier = "teamchat-${local.env}"

  engine         = "postgres"
  engine_version = "16"
  instance_class = var.db_instance_class

  allocated_storage = 20
  storage_type      = "gp3"
  storage_encrypted = true

  db_name  = var.db_name
  username = var.db_username
  password = random_password.db.result
  # Mot de passe genere par Terraform et stocke dans SSM (pas de Secrets Manager : cout).
  manage_master_user_password = false

  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [aws_security_group.rds.id]
  publicly_accessible    = false
  multi_az               = false

  backup_retention_period    = 7
  auto_minor_version_upgrade = true

  performance_insights_enabled = false

  deletion_protection       = var.db_deletion_protection
  skip_final_snapshot       = var.db_skip_final_snapshot
  final_snapshot_identifier = var.db_skip_final_snapshot ? null : "teamchat-${local.env}-final"
}
