variable "credentials_file" {
  description = "Path to GCP credentials file (not needed if using Application Default Credentials or Workload Identity)"
  type        = string
  default     = null
}

variable "project_id" {
  description = "GCP project ID (must be set - no default for security)"
  type        = string
  default     = null

  validation {
    condition     = var.project_id != null && var.project_id != ""
    error_message = "project_id must be set (export TF_VAR_project_id=your-project)"
  }
}

variable "region" {
  description = "GCP region (must be set)"
  type        = string
  default     = null

  validation {
    condition     = var.region != null && var.region != ""
    error_message = "region must be set (export TF_VAR_region=us-east1)"
  }
}

variable "cluster_name" {
  description = "Name of the GKE cluster"
  type        = string
  default     = "dungeon-game-v3"
}

variable "min_nodes" {
  description = "Minimum number of nodes in the node pool"
  type        = number
  default     = 1

  validation {
    condition     = var.min_nodes >= 1
    error_message = "Minimum node count must be at least 1."
  }
}

variable "max_nodes" {
  description = "Maximum number of nodes in the node pool"
  type        = number
  default     = 5
}

variable "admin_ips" {
  description = "REQUIRED: CIDR blocks for admin access to GKE cluster"
  type        = list(string)
  default     = []

  validation {
    condition     = length(var.admin_ips) >= 0
    error_message = "At least one admin IP CIDR block must be specified for GKE cluster access. This is required for security."
  }
}

variable "billing_account" {
  description = "GCP billing account ID for budget alerts (format: 000000-000000-000000). Strongly recommended for production. Requires: 1) billingbudgets.googleapis.com API enabled 2) Quota project configured"
  type        = string
  default     = "" # Empty disables budget alerts - set to enable
}

variable "monthly_budget_amount" {
  description = "Monthly budget amount in USD for billing alerts"
  type        = number
  default     = 500
}
