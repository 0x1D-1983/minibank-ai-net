# MiniBank

A Microsoft Agent Framework assistant over a small banking domain. Lookups use read-only tools. Deposits, withdrawals, and transfers are not model-invoked; they run only after a workflow approval step.

Solution: `MiniBank.AI.slnx`. Target `net10.0`.

## Layout

| Project | Owns |
|---|---|
| `MiniBank.Domain` | Accounts, exceptions, locks. No IO, hosts, or ASP.NET. |
| `MiniBank.Repositories` | `IAccountRepository` and `PostgresAccountRepository`. |
| `MiniBank.Services` | `Bank` (the only write path) and `IAuditLogger`. |
| `MiniBank.AI` | Agents, tools, `BankingWorkflow`, telemetry. |
| `MiniBank.Console` / `MiniBank.Api` | Thin hosts: composition, config, CLI/HTTP. |
| `MiniBank.AI.Tests` | Query, workflow, and owner-resolution tests. |

## How to change this repo

- Treat the GitHub issue (or PR description) as the spec. Stay inside its scope and out-of-scope list. No drive-by refactors or dependency upgrades.
- Prefer composing `BankingWorkflow` and `Bank` over new abstractions or a second banking layer in a host.
- Thin hosts copy Console: Serilog from configuration, then `AddMiniBankTracing(configuration, "<service name>")` with a distinct name (`MiniBank.Console`, `MiniBank.Api`). Do not invent a parallel telemetry stack.
- Default `IWriteApprover` is `AutoApprover`. Do not change workflow/tool/approval behaviour unless the spec says to.
- Update the README only when the spec requires it (how to run, example request).
- Leave CI test jobs commented out unless the spec asks to re-enable them.
- Do not add auth, persistence, or extra HTTP resources unless the spec says to.

## Verify

```bash
dotnet restore MiniBank.AI.slnx
dotnet build MiniBank.AI.slnx --configuration Release
```

Do not add or run Ollama-backed tests unless asked. Ollama may be unavailable (`http://localhost:11434`, model `qwen2.5:1.5b-instruct`).

## Git

Never commit to `main`. Do not amend pushed commits, force-push, skip hooks, or commit secrets / `_issues/` drafts unless that *is* the requested change.
