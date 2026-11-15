# Arret / demarrage automatique de l'EC2 et de RDS (ressources taguees AutoSchedule=true).
#   23h00 : stop   |   08h00 : start   |   toutes les heures : enforce (RDS redemarre seul apres 7 jours d'arret)
# Heures et fuseau : variables scheduler_*. var.scheduler_enabled = false desactive tout.

locals {
  scheduler_name = "teamchat-${local.env}-scheduler"
}

data "archive_file" "scheduler" {
  type        = "zip"
  source_file = "${path.module}/lambda/scheduler/handler.py"
  output_path = "${path.module}/.build/scheduler.zip"
}

resource "aws_cloudwatch_log_group" "scheduler" {
  name              = "/aws/lambda/${local.scheduler_name}"
  retention_in_days = 14
}

# ---------------- Role de la Lambda : limite aux ressources taguees ----------------

data "aws_iam_policy_document" "lambda_assume" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "scheduler" {
  name               = local.scheduler_name
  assume_role_policy = data.aws_iam_policy_document.lambda_assume.json
}

data "aws_iam_policy_document" "scheduler" {
  statement {
    sid       = "Logs"
    actions   = ["logs:CreateLogStream", "logs:PutLogEvents"]
    resources = ["${aws_cloudwatch_log_group.scheduler.arn}:*"]
  }

  # DescribeInstances n'accepte pas de ressource ciblee ; la Lambda filtre par tag.
  statement {
    sid       = "Ec2Describe"
    actions   = ["ec2:DescribeInstances"]
    resources = ["*"]
  }

  statement {
    sid       = "Ec2StartStopTagged"
    actions   = ["ec2:StartInstances", "ec2:StopInstances"]
    resources = ["arn:aws:ec2:${var.aws_region}:${local.account_id}:instance/*"]

    condition {
      test     = "StringEquals"
      variable = "aws:ResourceTag/AutoSchedule"
      values   = ["true"]
    }
  }

  statement {
    sid       = "RdsDescribe"
    actions   = ["rds:DescribeDBInstances", "rds:ListTagsForResource"]
    resources = ["arn:aws:rds:${var.aws_region}:${local.account_id}:db:*"]
  }

  statement {
    sid       = "RdsStartStopTagged"
    actions   = ["rds:StartDBInstance", "rds:StopDBInstance"]
    resources = ["arn:aws:rds:${var.aws_region}:${local.account_id}:db:*"]

    condition {
      test     = "StringEquals"
      variable = "aws:ResourceTag/AutoSchedule"
      values   = ["true"]
    }
  }

  statement {
    sid       = "Notify"
    actions   = ["sns:Publish"]
    resources = [aws_sns_topic.alerts.arn]
  }
}

resource "aws_iam_role_policy" "scheduler" {
  name   = local.scheduler_name
  role   = aws_iam_role.scheduler.id
  policy = data.aws_iam_policy_document.scheduler.json
}

# ---------------- Lambda ----------------

resource "aws_lambda_function" "scheduler" {
  function_name    = local.scheduler_name
  description      = "Arrete et demarre l'EC2 et RDS (tag AutoSchedule=true)"
  role             = aws_iam_role.scheduler.arn
  runtime          = "python3.13"
  handler          = "handler.handler"
  filename         = data.archive_file.scheduler.output_path
  source_code_hash = data.archive_file.scheduler.output_base64sha256
  architectures    = ["arm64"]
  memory_size      = 128
  timeout          = 840 # 14 min : attente du demarrage de RDS (action start)

  environment {
    variables = {
      SNS_TOPIC_ARN    = aws_sns_topic.alerts.arn
      TZ_NAME          = var.scheduler_timezone
      START_HOUR       = tostring(var.scheduler_start_hour)
      STOP_HOUR        = tostring(var.scheduler_stop_hour)
      RDS_WAIT_SECONDS = "600"
    }
  }

  depends_on = [aws_cloudwatch_log_group.scheduler, aws_iam_role_policy.scheduler]
}

# ---------------- EventBridge Scheduler ----------------

data "aws_iam_policy_document" "scheduler_invoke_assume" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["scheduler.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "scheduler_invoke" {
  name               = "${local.scheduler_name}-invoke"
  assume_role_policy = data.aws_iam_policy_document.scheduler_invoke_assume.json
}

data "aws_iam_policy_document" "scheduler_invoke" {
  statement {
    actions   = ["lambda:InvokeFunction"]
    resources = [aws_lambda_function.scheduler.arn]
  }
}

resource "aws_iam_role_policy" "scheduler_invoke" {
  name   = "invoke-scheduler-lambda"
  role   = aws_iam_role.scheduler_invoke.id
  policy = data.aws_iam_policy_document.scheduler_invoke.json
}

locals {
  scheduler_schedules = {
    stop = {
      expression = "cron(0 ${var.scheduler_stop_hour} * * ? *)"
      action     = "stop"
    }
    start = {
      expression = "cron(0 ${var.scheduler_start_hour} * * ? *)"
      action     = "start"
    }
    enforce = {
      expression = var.scheduler_enforce_rate
      action     = "enforce"
    }
  }
}

resource "aws_scheduler_schedule" "main" {
  for_each = local.scheduler_schedules

  name                         = "${local.scheduler_name}-${each.key}"
  description                  = "TeamChat : ${each.value.action}"
  state                        = var.scheduler_enabled ? "ENABLED" : "DISABLED"
  schedule_expression          = each.value.expression
  schedule_expression_timezone = var.scheduler_timezone

  flexible_time_window {
    mode = "OFF"
  }

  target {
    arn      = aws_lambda_function.scheduler.arn
    role_arn = aws_iam_role.scheduler_invoke.arn
    input    = jsonencode({ action = each.value.action })

    retry_policy {
      maximum_retry_attempts = 2
    }
  }
}
