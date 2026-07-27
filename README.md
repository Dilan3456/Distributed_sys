# Distributed Systems Lab — Kafka + Kubernetes + Istio + Helm + .NET + Argo CD

A hands-on lab for understanding distributed systems. Two .NET services
(a Producer web API and a Consumer worker) talk through Kafka, run on
Kubernetes, are packaged with Helm, meshed with Istio, and deployed via
GitOps with Argo CD.

```
ds-lab/
├── DsLab.sln                     .NET solution
├── src/
│   ├── Shared/                   shared event model + KafkaSettings
│   ├── Producer/                 web API -> publishes to Kafka
│   └── Consumer/                 worker -> consumes from Kafka (+ /metrics)
├── charts/ds-app/                Helm chart for both services
├── k8s/
│   ├── kafka/                    Strimzi Kafka cluster + topic
│   ├── istio/                    canary + fault-injection configs
│   └── argocd/                   Argo CD Application
└── .github/workflows/ci.yml      build images + bump chart (GitOps)
```

---

## Prerequisites

```bash
brew install dotnet kind kubectl helm istioctl k9s
# Docker Desktop running (you already have it)
```

---

## Part 1 — Build & run the .NET solution locally

```bash
dotnet build                          # builds all 3 projects
dotnet run --project src/Producer     # API on http://localhost:5xxx
```

`appsettings.json` holds config; `appsettings.Development.json` overrides it
locally. In the cluster, the Helm chart overrides the same keys with env vars
(`Kafka__BootstrapServers`) — same image, different config.

---

## Part 2 — Cluster

You already have Docker Desktop's single-node cluster. For the multi-node
experiments (rescheduling, node drain) use kind instead:

```bash
kind create cluster --name ds-lab --config - <<EOF
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
nodes:
  - role: control-plane
  - role: worker
  - role: worker
EOF
```

---

## Part 3 — Kafka via Strimzi

```bash
kubectl create namespace kafka
helm repo add strimzi https://strimzi.io/charts/
helm install strimzi strimzi/strimzi-kafka-operator -n kafka

# Wait for the operator, then create the cluster + topic:
kubectl apply -f k8s/kafka/kafka-cluster.yaml
kubectl wait kafka/ds-cluster -n kafka --for=condition=Ready --timeout=300s
kubectl apply -f k8s/kafka/kafka-topic.yaml
```

---

## Part 4 — Istio

```bash
istioctl install --set profile=demo -y
kubectl label namespace default istio-injection=enabled

# Observability addons:
kubectl apply -f https://raw.githubusercontent.com/istio/istio/release-1.24/samples/addons/prometheus.yaml
kubectl apply -f https://raw.githubusercontent.com/istio/istio/release-1.24/samples/addons/kiali.yaml
kubectl apply -f https://raw.githubusercontent.com/istio/istio/release-1.24/samples/addons/grafana.yaml
istioctl dashboard kiali
```

---

## Part 5 — Build images into the cluster

```bash
# Build with Docker, then load into kind (skip the load step on Docker Desktop):
docker build -t producer:dev -f src/Producer/Dockerfile src
docker build -t consumer:dev -f src/Consumer/Dockerfile src
kind load docker-image producer:dev consumer:dev --name ds-lab
```

---

## Part 6 — Deploy with Helm (push model, to learn it first)

```bash
helm upgrade --install ds-app ./charts/ds-app \
  --set image.repository=consumer --set image.tag=dev \
  --wait
kubectl get pods
```

`--install` is idempotent; `--wait` blocks until pods are healthy.

---

## Part 7 — GitOps with Argo CD (the real target)

```bash
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
kubectl port-forward svc/argocd-server -n argocd 8080:443

# Edit k8s/argocd/application.yaml -> set repoURL to your repo, then:
kubectl apply -f k8s/argocd/application.yaml
```

Now the flow is: push code -> CI builds images and bumps `values.yaml` ->
Argo CD detects the commit -> Argo deploys. Your pipeline never touches the
cluster.

---

## The experiments (where the learning happens)

1. **Publish & consume** — port-forward the producer, POST an order, watch it
   appear in a consumer's logs:
   ```bash
   kubectl port-forward svc/producer 8080:80
   curl -X POST localhost:8080/orders \
     -H 'Content-Type: application/json' \
     -d '{"customer":"dilan","amount":42.50}'
   kubectl logs -l app=consumer --tail=20
   ```
2. **Partitioning & consumer groups** — topic has 3 partitions, consumer has
   3 replicas -> each gets one partition. Scale to 5 and watch two sit idle.
3. **Rebalancing** — `kubectl delete pod` a consumer mid-stream; watch the
   group rebalance and another replica pick up its partition.
4. **Canary** — set `canary.enabled=true`, deploy a v2 consumer, apply
   `k8s/istio/virtualservice-canary.yaml`, watch the 90/10 split in Kiali.
5. **Fault injection** — apply `k8s/istio/virtualservice-fault.yaml` and watch
   latency/errors ripple through Grafana.
6. **Autoscaling** — flood the producer, watch the HPA add consumer pods.
7. **GitOps self-heal** — `kubectl delete deployment consumer`; watch Argo CD
   recreate it from Git.

---

## Interview angles this covers

- **SLIs/SLOs**: the `/metrics` counters (`orders_processed_total`,
  `orders_failed_total`) are your success-rate SLI.
- **Four golden signals**: latency/traffic/errors/saturation all visible in
  Grafana + Kiali.
- **Push vs pull CD**: you built both — explain why pull (Argo) is more secure.
- **Zero-downtime**: rolling updates via Helm + readiness probes.
