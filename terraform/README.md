# Terraform Quick Start Guide

## Prerequisites

- gcloud CLI installed and configured
- Terraform >= 1.0 installed
- GCP project with billing enabled

## Setup

1. Authenticate with GCP:
   ```bash
   gcloud auth application-default login
   ```

2. Source the setup script (sets project_id, region, and detects your IP):
   ```bash
   source setup-env.sh
   ```

## Commands

### Plan
```bash
terraform plan
```

### Apply
```bash
terraform apply
```

### Destroy
```bash
terraform destroy
```

## Manual Setup Required

Before first deployment, manually configure:

1. **Enable KMS encryption** on state bucket (if not already):
   ```bash
   gsutil versioning set on gs://dungeon-game-prod-terraform-state
   ```

2. **Get cluster credentials**:
   ```bash
   gcloud container clusters get-credentials dungeon-game-v3 --region=us-east1
   ```

3. **Create Kubernetes secrets** (after terraform apply):
   ```bash
   kubectl create secret generic dungeon-secrets \
     --from-literal=db-password=$(gcloud secrets versions access latest --secret=dungeon-db-password) \
     --from-literal=redis-password=$(gcloud secrets versions access latest --secret=dungeon-redis-auth)
   ```

4. **Create Kubernetes configmaps** (get IPs from terraform output):
   ```bash
   kubectl create configmap dungeon-redis-config \
     --from-literal=host=$(terraform output -raw redis_host) \
     --from-literal=port=6379
   kubectl create configmap dungeon-db-config \
     --from-literal=host=$(terraform output -raw postgres_private_ip)
   ```

5. **Deploy application**:
   ```bash
   kubectl apply -f deployment.yaml
   ```

## Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| project_id | yes | - | GCP project ID |
| region | yes | - | GCP region |
| admin_ips | yes | - | Admin CIDR blocks for GKE access |
| cluster_name | no | dungeon-game-v3 | GKE cluster name |
| min_nodes | no | 2 | Minimum node count |
| max_nodes | no | 5 | Maximum node count |
| billing_account | no | "" | GCP billing account ID for budget alerts |
| monthly_budget_amount | no | 500 | Monthly budget in USD |
