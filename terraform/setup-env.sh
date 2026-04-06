#!/bin/bash

export TF_VAR_project_id="${TF_VAR_project_id:-dungeon-game-prod}"
export TF_VAR_region="${TF_VAR_region:-us-east1}"

if [[ "$TF_VAR_project_id" != "dungeon-game-prod" ]]; then
  echo "WARNING: TF_VAR_project_id does not match the configured state bucket"
  echo "   State bucket: dungeon-game-prod-terraform-state"
fi

echo "Using project: $TF_VAR_project_id"
echo "Using region: $TF_VAR_region"

echo ""
echo "Detecting your public IP..."
MY_IP=$(curl -s https://api.ipify.org)

if [[ ! "$MY_IP" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "ERROR: Invalid IP detected: $MY_IP"
  exit 1
fi

if [[ "$MY_IP" =~ ^(10\.|172\.(1[6-9]|2[0-9]|3[01])\.|192\.168\.|127\.|0\.|255\.) ]]; then
  echo "ERROR: Private/reserved IP detected: $MY_IP"
  exit 1
fi

echo "Your IP: $MY_IP"
export TF_VAR_admin_ips="[\"$MY_IP/32\"]"

echo ""
echo "Validating Terraform state encryption..."
STATE_BUCKET="gs://dungeon-game-prod-terraform-state"

KMS_KEY=$(gsutil kms encryption "$STATE_BUCKET" 2>/dev/null || echo "")
if [[ -z "$KMS_KEY" ]]; then
  echo "WARNING: Terraform state bucket is NOT encrypted with KMS!"
  echo "   Run these commands to enable encryption:"
  echo "   1. gcloud services enable cloudkms.googleapis.com --project=$TF_VAR_project_id"
  echo "   2. gcloud kms keyrings create dungeon-keyring --location=$TF_VAR_region --project=$TF_VAR_project_id"
  echo "   3. gcloud kms keys create terraform-state-key --keyring=dungeon-keyring --location=$TF_VAR_region --project=$TF_VAR_project_id --purpose=encryption"
  echo "   4. gcloud kms keys add-iam-policy-binding terraform-state-key --keyring=dungeon-keyring --location=$TF_VAR_region --project=$TF_VAR_project_id --member=\"serviceAccount:service-974397495020@gs-project-accounts.iam.gserviceaccount.com\" --role=\"roles/cloudkms.cryptoKeyEncrypterDecrypter\""
  echo "   5. gcloud storage buckets update $STATE_BUCKET --default-encryption-key=projects/$TF_VAR_project_id/locations/$TF_VAR_region/keyRings/dungeon-keyring/cryptoKeys/terraform-state-key"
else
  echo "Terraform state bucket is encrypted with KMS key:"
  echo "   $KMS_KEY"
fi

VERSIONING=$(gsutil versioning get "$STATE_BUCKET" 2>/dev/null | grep -o "Enabled\|Suspended" || echo "")
if [[ "$VERSIONING" != "Enabled" ]]; then
  echo "WARNING: Terraform state bucket versioning is NOT enabled!"
  echo "   Run: gsutil versioning set on $STATE_BUCKET"
else
  echo "Terraform state bucket versioning is enabled"
fi

echo ""
echo "Environment variables set:"
echo "   TF_VAR_project_id=$TF_VAR_project_id"
echo "   TF_VAR_region=$TF_VAR_region"
echo "   TF_VAR_admin_ips=$TF_VAR_admin_ips"
echo ""
echo "To apply Terraform, run:"
echo "   terraform plan"
echo "   terraform apply"
echo ""
echo "To use kubectl, run:"
echo "   gcloud container clusters get-credentials dungeon-game-v3 --region=$TF_VAR_region"
echo ""
echo "Security checks complete!"
