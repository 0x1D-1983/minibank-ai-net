# MiniBank AI runtime (Kubernetes)

> **Local API key:** before testing the gateway, put a throwaway key in `skaffold.env` (`AI_API_KEY`) and the matching `exact` value in `gateway/envoy.yaml` (RBAC `x-api-key`). The committed files leave it empty so GitHub secret scanning does not flag the repo. Do not commit the key.

First version of an in-cluster model runtime. MiniBank keeps talking Ollama’s HTTP API; this stack is the practice environment instead of the standalone Ollama app.

## Target shape

```
                  Developer
                      │
                      ▼
              Internal AI API
                      │
             authentication
             rate limiting
             routing
                      │
                      ▼
                 Kubernetes
                      │
              inference scheduler
                      │
       ┌──────────────┼──────────────┐
       ▼              ▼              ▼
   GPU cluster     GPU cluster    GPU cluster
       │              │              │
   vLLM/TGI/etc.   vLLM/TGI/etc.   vLLM/TGI/etc.
       │              │              │
    Llama          qwen2.5:1.5b-instruct           Mistral
```

## What v1 actually deploys

| Target box | v1 stand-in |
|---|---|
| Developer | MiniBank.Api / curl on your machine |
| Internal AI API | Envoy `ai-gateway` (`x-api-key`, 20 req/min, model routes) |
| Inference scheduler | Kubernetes Service + Envoy clusters (`model-catalog`) |
| GPU cluster + vLLM/TGI | One CPU Ollama worker (no GPU required on Docker Desktop / kind / minikube) |
| qwen2.5:1.5b-instruct | Live: `ollama` Deployment, model pulled onto a PVC |
| Llama / Mistral | Routed, not deployed: `501` until you add those workers |

```
Developer ──:11434──► ollama Service ──► Ollama (qwen2.5:1.5b-instruct)
     │
     └──:8080──► ai-gateway (auth, rate limit, route) ──► ollama Service
```

Use `:11434` so MiniBank’s existing `Ollama:Endpoint` keeps working. Use `:8080` to practise the internal AI API.

## Prerequisites

- A local cluster (Docker Desktop Kubernetes, kind, or minikube)
- [Skaffold](https://skaffold.dev/)
- Enough RAM for a 1.5B CPU model (about 4–8 Gi)

From this directory:

```bash
set -a && source skaffold.env && set +a
skaffold run
```

`run` deploys and exits; the namespace and model PVC stay. First start pulls `qwen2.5:1.5b-instruct` onto the PVC (several minutes). The Ollama pod is not Ready until `ollama show` succeeds.

Do not use `skaffold dev` unless you want a file-watch loop. It has no image to build here (`No tags generated` is expected). On exit it deletes everything it created, including the PVC, so the next start re-downloads the model. If you do want logs and port-forwards:

```bash
skaffold dev --cleanup=false
```

Then Ctrl+C leaves the cluster as-is. Tear down later with `skaffold delete`.

A short `pod has unbound immediate PersistentVolumeClaims` while Skaffold waits is normal: the cluster is provisioning the models volume. It should go Ready within about a minute. If it stays Pending, the cluster has no default StorageClass.

Without Skaffold:

```bash
kubectl apply -k ollama/
kubectl apply -k gateway/
```

## Talk to the runtime

`skaffold run` also port-forwards `ollama` to `localhost:11434` and `ai-gateway` to `localhost:8080` if you pass `--port-forward`. `skaffold dev --cleanup=false` does that automatically.

Without port-forward:

```bash
kubectl -n ai-runtime port-forward svc/ollama 11434:11434
kubectl -n ai-runtime port-forward svc/ai-gateway 8080:8080
```

Ollama (MiniBank unchanged — `appsettings.json` already uses `http://localhost:11434`):

```bash
curl http://localhost:11434/api/tags
```

Internal AI API (API key from `skaffold.env`):

```bash
curl -sS http://localhost:8080/api/tags \
  -H "x-api-key: ${AI_API_KEY}"

curl -sS http://localhost:8080/v1/models/llama \
  -H "x-api-key: ${AI_API_KEY}"
```

The llama/mistral paths return `501` until those backends exist. Missing or wrong `x-api-key` returns `403`. More than 20 requests per minute returns `429`.

## Layout

Same Skaffold split as the Book API bundle: one module per component, root `skaffold.yml` + `skaffold.env`.

```
k8s/
  skaffold.yml          # composer
  skaffold.env          # local URLs, API key, model name
  ollama/               # inference worker (creates namespace ai-runtime)
  gateway/              # Internal AI API (Envoy)
```

Change the served model in `ollama/configmap.yaml` (`OLLAMA_MODEL`) and keep `skaffold.env` in sync. Change the API key in `gateway/envoy.yaml` (RBAC `exact` match) and `skaffold.env`.

## Next (still this architecture)

- Add `llama-vllm` and `mistral-tgi` Deployments; replace the `501` routes with real Envoy clusters (see comments in `gateway/envoy.yaml`).
- Request `nvidia.com/gpu` and node selectors on those workers.
- Replace the in-Envoy API key with OIDC / `ext_authz`.
- Replace Service round-robin with a real scheduler (KServe or Gateway API Inference Extension).
- Terminate TLS on the gateway, as the Book API edge Envoy does.
