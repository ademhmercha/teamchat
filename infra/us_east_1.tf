
locals {
  us_east_1_enabled = var.enable_us_east_1_resources
}

resource "aws_acm_certificate" "cloudfront" {
  count    = local.us_east_1_enabled && var.custom_domain != "" ? 1 : 0
  provider = aws.us_east_1

  domain_name       = var.custom_domain
  validation_method = "DNS"

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_sns_topic" "alerts_us_east_1" {
  count    = local.us_east_1_enabled ? 1 : 0
  provider = aws.us_east_1

  name = "teamchat-${local.env}-alerts"

  lifecycle {
    precondition {
      condition     = var.alert_email != ""
      error_message = "alert_email est obligatoire quand enable_us_east_1_resources = true."
    }
  }
}

resource "aws_sns_topic_subscription" "alerts_us_east_1_email" {
  count    = local.us_east_1_enabled ? 1 : 0
  provider = aws.us_east_1

  topic_arn = aws_sns_topic.alerts_us_east_1[0].arn
  protocol  = "email"
  endpoint  = var.alert_email
}

resource "aws_cloudwatch_metric_alarm" "cloudfront_5xx" {
  count    = local.us_east_1_enabled ? 1 : 0
  provider = aws.us_east_1

  alarm_name          = "teamchat-${local.env}-cloudfront-5xx"
  alarm_description   = "CloudFront 5xx error rate above 5 percent"
  namespace           = "AWS/CloudFront"
  metric_name         = "5xxErrorRate"
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 2
  threshold           = 5
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = [aws_sns_topic.alerts_us_east_1[0].arn]

  dimensions = {
    DistributionId = aws_cloudfront_distribution.main.id
    Region         = "Global"
  }
}
