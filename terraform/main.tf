terraform {
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 7.0"
    }
    google-beta = {
      source  = "hashicorp/google-beta"
      version = "~> 7.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.0"
    }
    local = {
      source  = "hashicorp/local"
      version = "~> 2.0"
    }
    time = {
      source  = "hashicorp/time"
      version = "~> 0.12"
    }
    null = {
      source  = "hashicorp/null"
      version = "~> 3.0"
    }
  }
  required_version = ">= 1.0"
}

resource "null_resource" "verify_state_encryption" {
  provisioner "local-exec" {
    command = <<-EOT
      echo "Verifying Terraform state bucket encryption..."
      ENCRYPTION=$(gsutil kms encryption gs://dungeon-game-prod-terraform-state 2>/dev/null || echo "")
      if [ -z "$ENCRYPTION" ]; then
        echo "CRITICAL ERROR: Terraform state bucket is NOT encrypted with Cloud KMS!"
        echo "   Run these commands to enable encryption:"
        echo "   1. gcloud services enable cloudkms.googleapis.com --project=dungeon-game-prod"
        echo "   2. gcloud kms keyrings create dungeon-keyring --location=us-central1 --project=dungeon-game-prod"
        echo "   3. gcloud kms keys create terraform-state-key --keyring=dungeon-keyring --location=us-central1 --project=dungeon-game-prod --purpose=encryption"
        echo "   4. gcloud kms keys add-iam-policy-binding terraform-state-key --keyring=dungeon-keyring --location=us-central1 --project=dungeon-game-prod --member='serviceAccount:service-974397495020@gs-project-accounts.iam.gserviceaccount.com' --role='roles/cloudkms.cryptoKeyEncrypterDecrypter'"
        echo "   5. gcloud storage buckets update gs://dungeon-game-prod-terraform-state --default-encryption-key=projects/dungeon-game-prod/locations/us-central1/keyRings/dungeon-keyring/cryptoKeys/terraform-state-key"
        exit 1
      fi
      echo "Terraform state bucket is encrypted with KMS key:"
      echo "   $ENCRYPTION"
    EOT
  }

  triggers = {
    always_run = timestamp()
  }
}

resource "random_password" "db_password" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"

  lifecycle {
    ignore_changes = all
  }
}

resource "google_compute_network" "vpc" {
  name                    = "dungeon-vpc"
  auto_create_subnetworks = false
  routing_mode            = "REGIONAL"
}

resource "google_project_service" "gke_api" {
  service            = "container.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "sql_api" {
  service            = "sqladmin.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "redis_api" {
  service            = "redis.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "secret_manager_api" {
  service            = "secretmanager.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "monitoring_api" {
  service            = "monitoring.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "logging_api" {
  service            = "logging.googleapis.com"
  disable_on_destroy = false
}

resource "google_service_account" "gke_workload" {
  account_id   = "dungeon-gke-workload"
  display_name = "GKE Workload Identity"
  description  = "Service account for GKE workload to access GCP services"
}

resource "google_project_iam_member" "workload_secret_manager" {
  project = var.project_id
  role    = "roles/secretmanager.secretAccessor"
  member  = "serviceAccount:${google_service_account.gke_workload.email}"
}

resource "google_project_iam_member" "workload_cloud_sql" {
  project = var.project_id
  role    = "roles/cloudsql.client"
  member  = "serviceAccount:${google_service_account.gke_workload.email}"
}

resource "google_billing_budget" "monthly_budget" {
  count = var.billing_account != "" ? 1 : 0

  billing_account = var.billing_account
  display_name    = "Dungeon Game Monthly Budget"

  amount {
    specified_amount {
      currency_code = "USD"
      units         = var.monthly_budget_amount
    }
  }

  threshold_rules {
    threshold_percent = 50
    spend_basis       = "CURRENT_SPEND"
  }
  threshold_rules {
    threshold_percent = 80
    spend_basis       = "CURRENT_SPEND"
  }
  threshold_rules {
    threshold_percent = 100
    spend_basis       = "CURRENT_SPEND"
  }
}

resource "google_compute_subnetwork" "subnet" {
  name          = "dungeon-subnet"
  region        = var.region
  network       = google_compute_network.vpc.id
  ip_cidr_range = "10.0.0.0/24"

  private_ip_google_access = true

  secondary_ip_range {
    range_name    = "pod-range"
    ip_cidr_range = "10.1.0.0/16"
  }

  secondary_ip_range {
    range_name    = "service-range"
    ip_cidr_range = "10.2.0.0/16"
  }

  log_config {
    aggregation_interval = "INTERVAL_30_SEC"
    flow_sampling        = 0.5
    metadata             = "INCLUDE_ALL_METADATA"
  }
}

resource "google_compute_global_address" "redis_private_ip" {
  name          = "dungeon-redis-ip"
  address_type  = "INTERNAL"
  network       = google_compute_network.vpc.id
  prefix_length = 16
  purpose       = "VPC_PEERING"
}

resource "google_service_networking_connection" "redis_vpc_connection" {
  network                 = google_compute_network.vpc.id
  service                 = "servicenetworking.googleapis.com"
  reserved_peering_ranges = [google_compute_global_address.redis_private_ip.name]
}

resource "google_compute_firewall" "allow_health_check" {
  name          = "allow-grpc-health-check"
  network       = google_compute_network.vpc.id
  direction     = "INGRESS"
  source_ranges = ["35.191.0.0/16", "130.211.0.0/22"]

  allow {
    protocol = "tcp"
    ports    = ["8080", "5142"]
  }
}

resource "google_compute_firewall" "allow_pod_to_pod" {
  name          = "allow-pod-to-pod"
  network       = google_compute_network.vpc.id
  direction     = "INGRESS"
  source_ranges = ["10.1.0.0/16", "10.2.0.0/16"] # Pod and service ranges

  allow {
    protocol = "tcp"
  }
  allow {
    protocol = "udp"
  }
  allow {
    protocol = "icmp"
  }
}

resource "google_compute_firewall" "allow_gcp_apis" {
  name               = "allow-gcp-apis"
  network            = google_compute_network.vpc.id
  direction          = "EGRESS"
  priority           = 900
  destination_ranges = ["199.36.153.8/30"] # Restricted Google APIs range
  target_tags        = ["dungeon-game"]

  allow {
    protocol = "tcp"
    ports    = ["443"]
  }
}

resource "google_compute_firewall" "allow_cloud_sql" {
  name               = "allow-cloud-sql"
  network            = google_compute_network.vpc.id
  direction          = "EGRESS"
  priority           = 800
  destination_ranges = ["10.0.0.0/24"] # VPC subnet where Cloud SQL private IP resides
  target_tags        = ["dungeon-game"]

  allow {
    protocol = "tcp"
    ports    = ["5432"]
  }
}

resource "google_compute_security_policy" "dungeon_game_policy" {
  name = "dungeon-game-security-policy"

  rule {
    action   = "allow"
    priority = "2147483647"
    match {
      versioned_expr = "SRC_IPS_V1"
      config {
        src_ip_ranges = ["*"]
      }
    }
    description = "Default allow rule"
  }

  rule {
    action      = "rate_based_ban"
    priority    = "1000"
    description = "Rate limit excessive requests"

    match {
      expr {
        expression = "true"
      }
    }

    rate_limit_options {
      rate_limit_threshold {
        count        = 100
        interval_sec = 60
      }
      ban_duration_sec = 3600
      conform_action   = "allow"
      exceed_action    = "deny(429)"
      enforce_on_key   = "IP"
    }
  }

  rule {
    action      = "deny(403)"
    priority    = "1001"
    description = "Block SQL injection patterns"

    match {
      expr {
        expression = "evaluatePreconfiguredWaf('sqli-v33-stable', {'sensitivity': 2})"
      }
    }
    preview = true
  }

  rule {
    action      = "deny(403)"
    priority    = "1002"
    description = "Block XSS patterns"

    match {
      expr {
        expression = "evaluatePreconfiguredWaf('xss-v33-stable', {'sensitivity': 2})"
      }
    }
    preview = true
  }
}

resource "google_container_cluster" "primary" {
  name     = var.cluster_name
  location = var.region

  deletion_protection      = true
  remove_default_node_pool = true
  initial_node_count       = 1

  resource_labels = {
    env        = "dungeon-game"
    managed_by = "terraform"
  }

  networking_mode = "VPC_NATIVE"
  network         = google_compute_network.vpc.id
  subnetwork      = google_compute_subnetwork.subnet.id

  node_locations = ["us-east1-b", "us-east1-c", "us-east1-d"]

  workload_identity_config {
    workload_pool = "${var.project_id}.svc.id.goog"
  }

  addons_config {
    http_load_balancing {
      disabled = false
    }
    horizontal_pod_autoscaling {
      disabled = false
    }
    gcp_filestore_csi_driver_config {
      enabled = false
    }
  }

  release_channel {
    channel = "REGULAR"
  }

  private_cluster_config {
    enable_private_nodes    = true
    enable_private_endpoint = false
    master_ipv4_cidr_block  = "10.4.0.0/28"
  }

  master_authorized_networks_config {
    dynamic "cidr_blocks" {
      for_each = var.admin_ips
      content {
        cidr_block   = cidr_blocks.value
        display_name = "admin-access"
      }
    }
    gcp_public_cidrs_access_enabled = false
  }

  timeouts {
    create = "30m"
    update = "30m"
  }
}

resource "google_container_node_pool" "primary_nodes" {
  name     = "default-pool"
  location = var.region
  cluster  = google_container_cluster.primary.name

  autoscaling {
    min_node_count = var.min_nodes
    max_node_count = var.max_nodes
  }

  management {
    auto_repair  = true
    auto_upgrade = true
  }

  upgrade_settings {
    max_surge       = 1
    max_unavailable = 0
  }

  network_config {
    enable_private_nodes = true
    pod_range            = "pod-range"
  }

  node_config {
    machine_type = "n2-standard-2"
    disk_size_gb = 20
    disk_type    = "pd-ssd"
    preemptible  = false
    oauth_scopes = [
      "https://www.googleapis.com/auth/devstorage.read_only",
      "https://www.googleapis.com/auth/logging.write",
      "https://www.googleapis.com/auth/monitoring",
    ]
    labels = {
      env = "dungeon-game"
    }
    tags = ["dungeon-game"]
    metadata = {
      disable-legacy-endpoints = "true"
    }
  }
}

resource "google_sql_database_instance" "postgres" {
  name                = "dungeon-postgres-prod"
  region              = var.region
  database_version    = "POSTGRES_15"
  deletion_protection = true

  settings {
    tier              = "db-custom-2-4096"
    availability_type = "REGIONAL"

    backup_configuration {
      enabled                        = true
      start_time                     = "03:00"
      point_in_time_recovery_enabled = true
      transaction_log_retention_days = 7
    }

    maintenance_window {
      day          = 7
      hour         = 2
      update_track = "stable"
    }

    ip_configuration {
      ipv4_enabled    = false
      private_network = google_compute_network.vpc.id
      ssl_mode        = "ENCRYPTED_ONLY"
    }

    disk_autoresize = true
    disk_size       = 20
    disk_type       = "PD_SSD"

    user_labels = {
      env        = "dungeon-game"
      managed_by = "terraform"
    }
  }
}

resource "google_secret_manager_secret" "db_password_secret" {
  secret_id = "dungeon-db-password"

  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_version" "db_password_version" {
  secret_data = random_password.db_password.result
  secret      = google_secret_manager_secret.db_password_secret.id
  depends_on  = [google_secret_manager_secret.db_password_secret]

  lifecycle {
    ignore_changes = [secret_data]
  }
}

resource "google_secret_manager_secret" "redis_auth_secret" {
  secret_id = "dungeon-redis-auth"

  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_version" "redis_auth_version" {
  secret_data = google_redis_instance.redis.auth_string
  secret      = google_secret_manager_secret.redis_auth_secret.id
  depends_on  = [google_secret_manager_secret.redis_auth_secret, google_redis_instance.redis]

  lifecycle {
    ignore_changes = [secret_data]
  }
}

provider "kubernetes" {
  host                   = "https://${google_container_cluster.primary.endpoint}"
  cluster_ca_certificate = base64decode(google_container_cluster.primary.master_auth[0].cluster_ca_certificate)
  token                  = data.google_client_config.default.access_token
}

data "google_client_config" "default" {}

resource "google_service_account_iam_binding" "workload_identity_binding" {
  service_account_id = google_service_account.gke_workload.id
  role               = "roles/iam.workloadIdentityUser"

  members = [
    "serviceAccount:${var.project_id}.svc.id.goog[default/dungeon-gke-workload]"
  ]

  depends_on = [
    google_container_cluster.primary
  ]
}

output "cluster_name" {
  value = google_container_cluster.primary.name
}

output "cluster_location" {
  value = google_container_cluster.primary.location
}

output "cluster_endpoint" {
  value     = google_container_cluster.primary.endpoint
  sensitive = true
}

output "redis_host" {
  value = google_redis_instance.redis.host
}

output "redis_auth_secret_id" {
  value = google_secret_manager_secret.redis_auth_secret.id
}

output "postgres_private_ip" {
  value = google_sql_database_instance.postgres.private_ip_address
}

output "db_password_secret_id" {
  value = google_secret_manager_secret.db_password_secret.id
}
