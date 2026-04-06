# Local Kubernetes Setup

Quick guide to run the Dungeon Game stack locally using Kind + Helm.

## Prerequisites

- Docker
- kubectl
- kind
- helm

## Setup

### 1. Create Cluster

```bash
kind create cluster --name dungeon
```

### 2. Add Helm Repos

```bash
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo add containeroo https://charts.containeroo.ch
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo update
```

### 3. Install All Components via Helm

```bash
# Ingress Controller
helm install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx \
  --create-namespace \
  --wait

# Storage Provisioner
helm install local-path-provisioner containeroo/local-path-provisioner \
  --namespace local-path-storage \
  --create-namespace \
  --wait

# PostgreSQL
helm install postgres bitnami/postgresql \
  --set auth.username=dungeon_user \
  --set auth.password=dungeon_password \
  --set auth.database=dungeon_db \
  --wait

# Redis
helm install redis bitnami/redis \
  --set auth.password=redis_password \
  --wait
```

### 4. Build & Load Images

```bash
docker build -t dungeon-server:latest -f Dockerfile.server .
docker build -t dungeon-benchmark:latest -f Dockerfile.benchmark .

kind load docker-image dungeon-server:latest --name dungeon
kind load docker-image dungeon-benchmark:latest --name dungeon
```

### 5. Deploy Application

```bash
kubectl apply -f terraform/deployment-local.yaml
```

## Access Services

```bash
# Game Server (gRPC)
kubectl port-forward svc/dungeon-server 8080:8080

# Benchmark Dashboard
kubectl port-forward svc/dungeon-benchmark 9092:9092

# PostgreSQL
kubectl port-forward svc/postgres-postgresql 5432:5432

# Redis
kubectl port-forward svc/redis-master 6379:6379
```

## Useful Commands

```bash
# View all pods
kubectl get pods

# View Helm releases
helm list --all-namespaces

# View logs
kubectl logs <pod-name>

# Shell into pod
kubectl exec -it <pod-name> -- /bin/sh

# Scale deployment
kubectl scale deployment dungeon-server --replicas=3

# Upgrade a Helm release
helm upgrade ingress-nginx ingress-nginx/ingress-nginx -n ingress-nginx
```

## Cleanup

```bash
# Remove app
kubectl delete -f terraform/deployment-local.yaml

# Remove all Helm releases
helm uninstall postgres
helm uninstall redis
helm uninstall ingress-nginx -n ingress-nginx
helm uninstall local-path-provisioner -n local-path-storage

# Remove cluster
kind delete cluster --name dungeon
```
