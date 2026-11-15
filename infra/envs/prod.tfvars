environment = "prod"
aws_region  = "eu-north-1"

db_instance_class         = "db.t4g.micro"
rds_backup_retention_days = 1
db_deletion_protection    = true
db_skip_final_snapshot    = false

instance_type = "t3.micro"

enable_pr_checks = true

budget_name            = "team chat monthy budget"
budget_limit_usd       = "30"
import_existing_budget = true
