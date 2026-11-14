# VPC par defaut et ses sous-reseaux : pas de NAT Gateway, pas de VPC dedie (cout).
data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
}

# Liste de prefixes geree par AWS : adresses des serveurs d'origine de CloudFront.
data "aws_ec2_managed_prefix_list" "cloudfront" {
  name = "com.amazonaws.global.cloudfront.origin-facing"
}

# ---- EC2 (API) : port 80 depuis CloudFront uniquement, pas de SSH (Session Manager) ----

resource "aws_security_group" "ec2" {
  name_prefix = "teamchat-${local.env}-ec2-"
  description = "TeamChat API - HTTP from CloudFront only"
  vpc_id      = data.aws_vpc.default.id

  lifecycle {
    create_before_destroy = true
  }
}

# Attention : cette liste compte pour ~55 regles dans le quota (60 par defaut) du groupe.
resource "aws_vpc_security_group_ingress_rule" "ec2_http_from_cloudfront" {
  security_group_id = aws_security_group.ec2.id
  description       = "HTTP from CloudFront"
  prefix_list_id    = data.aws_ec2_managed_prefix_list.cloudfront.id
  ip_protocol       = "tcp"
  from_port         = 80
  to_port           = 80
}

resource "aws_vpc_security_group_egress_rule" "ec2_all" {
  security_group_id = aws_security_group.ec2.id
  description       = "Allow all outbound"
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "-1"
}

# ---- RDS : PostgreSQL uniquement depuis l'EC2 ----

resource "aws_security_group" "rds" {
  name_prefix = "teamchat-${local.env}-rds-"
  description = "TeamChat PostgreSQL - from EC2 only"
  vpc_id      = data.aws_vpc.default.id

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_vpc_security_group_ingress_rule" "rds_from_ec2" {
  security_group_id            = aws_security_group.rds.id
  description                  = "PostgreSQL from EC2"
  referenced_security_group_id = aws_security_group.ec2.id
  ip_protocol                  = "tcp"
  from_port                    = 5432
  to_port                      = 5432
}
