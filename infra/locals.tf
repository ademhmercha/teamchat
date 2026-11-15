data "aws_caller_identity" "current" {}

locals {
  account_id = data.aws_caller_identity.current.account_id
  env        = var.environment

  frontend_bucket_name = "teamchat-frontend-${local.env}-${local.account_id}"
  uploads_bucket_name  = "teamchat-uploads-${local.env}-${local.account_id}"

  artifacts_bucket_name = "teamchat-pipeline-artifacts-${local.env}-${local.account_id}"

  ssm_path      = "/teamchat/${local.env}"
  log_group     = "/teamchat/api"
  ecr_repo_name = "teamchat-api"
}
