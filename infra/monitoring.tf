# Supervision : alarmes CloudWatch -> SNS (alerts.tf), agent CloudWatch (memoire, disque), tableau de bord.
#
# La Lambda scheduler arrete l'EC2 et RDS la nuit : les metriques disparaissent alors. Toutes les alarmes
# utilisent treat_missing_data = "notBreaching" pour ne pas sonner chaque nuit.
# Aucune action "OK" : sinon un email a chaque redemarrage du matin.

locals {
  alarm_actions = [aws_sns_topic.alerts.arn]

  instance_id = aws_instance.api.id
  db_id       = aws_db_instance.main.identifier

  metrics_namespace = "TeamChat/${local.env}"
}

# ---------------- EC2 ----------------

# Panne materielle de l'hote AWS : notification ET recuperation automatique (redemarrage sur un autre hote,
# meme IP elastique et meme disque).
resource "aws_cloudwatch_metric_alarm" "ec2_status_system" {
  alarm_name          = "teamchat-${local.env}-ec2-status-check-system"
  alarm_description   = "Controle d'etat SYSTEME de l'EC2 en echec : recuperation automatique"
  namespace           = "AWS/EC2"
  metric_name         = "StatusCheckFailed_System"
  dimensions          = { InstanceId = local.instance_id }
  statistic           = "Maximum"
  period              = 60
  evaluation_periods  = 2
  threshold           = 0
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = concat(local.alarm_actions, ["arn:aws:automate:${var.aws_region}:ec2:recover"])
}

# Probleme dans le systeme d'exploitation de l'instance (noyau, reseau) : notification seulement.
resource "aws_cloudwatch_metric_alarm" "ec2_status_instance" {
  alarm_name          = "teamchat-${local.env}-ec2-status-check-instance"
  alarm_description   = "Controle d'etat INSTANCE de l'EC2 en echec"
  namespace           = "AWS/EC2"
  metric_name         = "StatusCheckFailed_Instance"
  dimensions          = { InstanceId = local.instance_id }
  statistic           = "Maximum"
  period              = 60
  evaluation_periods  = 3
  threshold           = 0
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = local.alarm_actions
}

resource "aws_cloudwatch_metric_alarm" "ec2_cpu" {
  alarm_name          = "teamchat-${local.env}-ec2-cpu-high"
  alarm_description   = "CPU de l'EC2 superieur a 80 % pendant 10 minutes"
  namespace           = "AWS/EC2"
  metric_name         = "CPUUtilization"
  dimensions          = { InstanceId = local.instance_id }
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 2
  threshold           = 80
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = local.alarm_actions
}

# Metriques de l'agent CloudWatch (namespace CWAgent) : voir plus bas.
resource "aws_cloudwatch_metric_alarm" "ec2_memory" {
  alarm_name          = "teamchat-${local.env}-ec2-memory-high"
  alarm_description   = "Memoire de l'EC2 superieure a 90 % pendant 10 minutes (1 Go : surveiller le swap)"
  namespace           = "CWAgent"
  metric_name         = "mem_used_percent"
  dimensions          = { InstanceId = local.instance_id }
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 2
  threshold           = 90
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = local.alarm_actions
}

resource "aws_cloudwatch_metric_alarm" "ec2_disk" {
  alarm_name          = "teamchat-${local.env}-ec2-disk-high"
  alarm_description   = "Disque racine de l'EC2 rempli a plus de 85 %"
  namespace           = "CWAgent"
  metric_name         = "disk_used_percent"
  dimensions          = { InstanceId = local.instance_id, path = "/" }
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 2
  threshold           = 85
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = local.alarm_actions
}

# ---------------- RDS ----------------

resource "aws_cloudwatch_metric_alarm" "rds_cpu" {
  alarm_name          = "teamchat-${local.env}-rds-cpu-high"
  alarm_description   = "CPU de RDS superieur a 80 % pendant 10 minutes"
  namespace           = "AWS/RDS"
  metric_name         = "CPUUtilization"
  dimensions          = { DBInstanceIdentifier = local.db_id }
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 2
  threshold           = 80
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = local.alarm_actions
}

resource "aws_cloudwatch_metric_alarm" "rds_storage" {
  alarm_name          = "teamchat-${local.env}-rds-free-storage-low"
  alarm_description   = "Moins de 2 Go d'espace libre sur RDS (20 Go alloues, sans autoscaling)"
  namespace           = "AWS/RDS"
  metric_name         = "FreeStorageSpace"
  dimensions          = { DBInstanceIdentifier = local.db_id }
  statistic           = "Minimum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 2 * 1024 * 1024 * 1024
  comparison_operator = "LessThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = local.alarm_actions
}

resource "aws_cloudwatch_metric_alarm" "rds_connections" {
  alarm_name          = "teamchat-${local.env}-rds-connections-high"
  alarm_description   = "Plus de 80 connexions ouvertes sur RDS (db.t4g.micro : ~110 maximum)"
  namespace           = "AWS/RDS"
  metric_name         = "DatabaseConnections"
  dimensions          = { DBInstanceIdentifier = local.db_id }
  statistic           = "Maximum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 80
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = local.alarm_actions
}

# ---------------- Disponibilite de l'API : erreurs dans les journaux ----------------
# Choix : filtre de metriques sur /teamchat/api, plutot qu'un health check Route 53.
#   - Filtre de metriques + alarme : ~0,10 $/mois (metrique personnalisee dans le quota gratuit des 10),
#     region eu-north-1, aucune ressource en us-east-1.
#   - Route 53 health check : ~0,50 a 2,50 $/mois selon les options (HTTPS, intervalle rapide), ET ses
#     metriques n'existent qu'en us-east-1 : alarme et sujet SNS y seraient necessaires, region que la SCP du
#     compte refuse. Il ne serait de toute facon pas utilisable ici.
# Limite : une panne totale (plus aucun journal) n'est pas vue par ce filtre ; elle l'est par les controles
# d'etat de l'EC2 ci-dessus. L'API doit journaliser en JSON (appsettings.Production.json).

resource "aws_cloudwatch_log_metric_filter" "api_errors" {
  name           = "teamchat-${local.env}-api-errors"
  log_group_name = aws_cloudwatch_log_group.api.name
  pattern        = "{ ($.LogLevel = \"Error\") || ($.LogLevel = \"Critical\") }"

  metric_transformation {
    name      = "ApiErrors"
    namespace = local.metrics_namespace
    value     = "1"
  }
}

resource "aws_cloudwatch_metric_alarm" "api_errors" {
  alarm_name          = "teamchat-${local.env}-api-errors"
  alarm_description   = "5 erreurs (Error ou Critical) ou plus en 5 minutes dans les journaux de l'API"
  namespace           = local.metrics_namespace
  metric_name         = aws_cloudwatch_log_metric_filter.api_errors.metric_transformation[0].name
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 5
  comparison_operator = "GreaterThanOrEqualToThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = local.alarm_actions
}

# ---------------- Agent CloudWatch : memoire et disque ----------------
# Installe et configure par SSM State Manager (pas de user_data : il est ignore sur l'instance existante, et
# l'association couvre aussi une instance recreee). Cible : toute instance taguee App=teamchat.
# L'agent reste installe quand le scheduler arrete l'instance ; l'association se reapplique au demarrage.

locals {
  # Hors de /teamchat/<env>/ : start.sh transforme chaque parametre de ce chemin en variable d'environnement.
  cloudwatch_agent_param = "/cloudwatch-agent/teamchat-${local.env}/config"
}

resource "aws_ssm_parameter" "cloudwatch_agent_config" {
  name = local.cloudwatch_agent_param
  type = "String"
  value = jsonencode({
    agent = { metrics_collection_interval = 60 }
    metrics = {
      namespace              = "CWAgent"
      append_dimensions      = { InstanceId = "$${aws:InstanceId}" }
      aggregation_dimensions = [["InstanceId"], ["InstanceId", "path"]]
      force_flush_interval   = 60
      metrics_collected = {
        mem  = { measurement = ["mem_used_percent"] }
        disk = { measurement = ["used_percent"], resources = ["/"] }
      }
    }
  })
}

resource "aws_ssm_association" "cloudwatch_agent_install" {
  name             = "AWS-ConfigureAWSPackage"
  association_name = "teamchat-${local.env}-cloudwatch-agent-install"

  parameters = {
    action = "Install"
    name   = "AmazonCloudWatchAgent"
  }

  targets {
    key    = "tag:App"
    values = ["teamchat"]
  }

  schedule_expression = "rate(7 days)"
}

resource "aws_ssm_association" "cloudwatch_agent_configure" {
  name             = "AmazonCloudWatch-ManageAgent"
  association_name = "teamchat-${local.env}-cloudwatch-agent-config"

  parameters = {
    action                        = "configure"
    mode                          = "ec2"
    optionalConfigurationSource   = "ssm"
    optionalConfigurationLocation = local.cloudwatch_agent_param
    optionalRestart               = "yes"
  }

  targets {
    key    = "tag:App"
    values = ["teamchat"]
  }

  # Reessaye souvent : au premier passage l'installation ci-dessus peut ne pas etre terminee.
  schedule_expression = "rate(30 minutes)"

  depends_on = [aws_ssm_association.cloudwatch_agent_install, aws_ssm_parameter.cloudwatch_agent_config]
}

# ---------------- Tableau de bord ----------------

locals {
  dashboard_widgets = [
    {
      type = "metric", x = 0, y = 0, width = 12, height = 6
      properties = {
        title  = "EC2 : CPU, memoire, disque (%)"
        region = var.aws_region
        view   = "timeSeries"
        stat   = "Average"
        period = 300
        yAxis  = { left = { min = 0, max = 100 } }
        metrics = [
          ["AWS/EC2", "CPUUtilization", "InstanceId", local.instance_id, { label = "CPU" }],
          ["CWAgent", "mem_used_percent", "InstanceId", local.instance_id, { label = "Memoire" }],
          ["CWAgent", "disk_used_percent", "InstanceId", local.instance_id, "path", "/", { label = "Disque /" }],
        ]
      }
    },
    {
      type = "metric", x = 12, y = 0, width = 12, height = 6
      properties = {
        title  = "RDS : CPU (%) et connexions"
        region = var.aws_region
        view   = "timeSeries"
        stat   = "Average"
        period = 300
        metrics = [
          ["AWS/RDS", "CPUUtilization", "DBInstanceIdentifier", local.db_id, { label = "CPU %" }],
          ["AWS/RDS", "DatabaseConnections", "DBInstanceIdentifier", local.db_id, { label = "Connexions", yAxis = "right" }],
        ]
      }
    },
    {
      type = "metric", x = 0, y = 6, width = 12, height = 6
      properties = {
        title   = "RDS : espace libre (octets)"
        region  = var.aws_region
        view    = "timeSeries"
        stat    = "Minimum"
        period  = 300
        metrics = [["AWS/RDS", "FreeStorageSpace", "DBInstanceIdentifier", local.db_id, { label = "Libre" }]]
        annotations = {
          horizontal = [{ label = "Seuil d'alerte (2 Go)", value = 2 * 1024 * 1024 * 1024 }]
        }
      }
    },
    {
      type = "metric", x = 12, y = 6, width = 12, height = 6
      properties = {
        title  = "CloudFront : requetes et erreurs"
        region = "us-east-1" # les metriques CloudFront n'existent qu'en us-east-1
        view   = "timeSeries"
        period = 300
        metrics = [
          ["AWS/CloudFront", "Requests", "DistributionId", aws_cloudfront_distribution.main.id, "Region", "Global", { stat = "Sum", label = "Requetes" }],
          ["AWS/CloudFront", "5xxErrorRate", "DistributionId", aws_cloudfront_distribution.main.id, "Region", "Global", { stat = "Average", label = "5xx %", yAxis = "right" }],
          ["AWS/CloudFront", "4xxErrorRate", "DistributionId", aws_cloudfront_distribution.main.id, "Region", "Global", { stat = "Average", label = "4xx %", yAxis = "right" }],
        ]
      }
    },
    {
      type = "metric", x = 0, y = 12, width = 12, height = 6
      properties = {
        title   = "API : erreurs journalisees (Error + Critical)"
        region  = var.aws_region
        view    = "timeSeries"
        stat    = "Sum"
        period  = 300
        metrics = [[local.metrics_namespace, "ApiErrors", { label = "Erreurs / 5 min" }]]
        annotations = {
          horizontal = [{ label = "Seuil d'alerte", value = 5 }]
        }
      }
    },
    {
      type = "log", x = 12, y = 12, width = 12, height = 6
      properties = {
        title  = "API : dernieres erreurs"
        region = var.aws_region
        view   = "table"
        query  = "SOURCE '${aws_cloudwatch_log_group.api.name}' | fields @timestamp, Category, Message | filter LogLevel in [\"Error\", \"Critical\"] | sort @timestamp desc | limit 20"
      }
    },
  ]
}

resource "aws_cloudwatch_dashboard" "main" {
  dashboard_name = "teamchat-${local.env}"
  dashboard_body = jsonencode({ widgets = local.dashboard_widgets })
}
