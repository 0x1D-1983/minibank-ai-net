# MiniBank AI

A Microsoft Agent Framework assistant for a small banking domain. Lookups go through read-only tools. Deposits, withdrawals, and transfers cannot be invoked by the model; they run only after a workflow approval step.

The bank itself lives in this repo: accounts, concurrency, persistence, and the `Bank` service. The console seeds an in-memory bank and chats through `BankingWorkflow`.

## Layout

| Project | Role |
|---|---|
| `MiniBank.Domain` | Accounts (`CurrentAccount`, `SavingsAccount`), exceptions, per-account locks |
| `MiniBank.Repositories` | `IAccountRepository` and `PostgresAccountRepository` |
| `MiniBank.Services` | `Bank` (deposit / withdraw / transfer) and `IAuditLogger` |
| `MiniBank.Auth` | `IAuthenticationService` and in-memory mock identity server |
| `MiniBank.AI` | Agents, tools, workflow, telemetry |
| `MiniBank.Api` | Minimal API host + Serilog + OpenTelemetry |
| `MiniBank.Console` | Interactive host + Serilog + OpenTelemetry |
| `MiniBank.AI.Tests` | Query-agent tests, workflow routing tests, and authorization unit tests |

Solution: `MiniBank.AI.slnx`.

## Banking domain

`Bank` is the only write path. It loads accounts from `IAccountRepository`, mutates them, persists, and audits.

| Type | Extra rule |
|---|---|
| `CurrentAccount` | Withdrawals may use an overdraft limit |
| `SavingsAccount` | Withdrawals cannot exceed the balance; can apply interest |

Account mutation is lock-gated (`AsyncFriendlyLock`). Transfers take both locks in account-number order via `Account.LockAllAsync` so two accounts cannot deadlock.

The console and tests use an in-memory repository. `PostgresAccountRepository` (Npgsql) is the durable implementation of the same interface.

## Prerequisites

- .NET 10
- [Ollama](https://ollama.com) at `http://localhost:11434`
- Model `qwen2.5:1.5b-instruct`

Standalone app:

```bash
ollama pull qwen2.5:1.5b-instruct
```

Or run the same model in Kubernetes instead (CPU worker, no standalone Ollama app): see [`k8s/README.md`](k8s/README.md). Port-forward `svc/ollama` to `localhost:11434` so host `appsettings.json` does not change.

Optional: an OTLP collector at `http://localhost:4317` (see `MiniBank.Console/appsettings.json`). Tracing can be turned off with `"Tracing": { "Enabled": false }`.

## Run

### Authentication

Both hosts require authentication. Users can only see and modify their own accounts. The system uses an in-memory mock identity server with three demo users matching the seeded accounts:

| Username | Password | Customer Name | Accounts |
|---|---|---|---|
| alice | password | Alice Example | 1234567890 |
| john | password | John Smith | 10001, 10002 |
| jane | password | Jane Doe | 20001 |

### Console

```bash
dotnet run --project MiniBank.Console
```

The console prompts for credentials before starting the chat loop:

```
MiniBank assistant.
Demo users: alice, john, jane (password: password)

Username: john
Password: ********
Welcome, John Smith! You can now ask questions about your accounts.

You: What is my balance?
```

After login, every question runs as that customer until the process exits. You can only see and manage accounts you own. Type `quit` (or `exit` / `q` / `bye`) to leave.

### API

```bash
dotnet run --project MiniBank.Api
```

The API serves at `http://localhost:5000` by default. It exposes:

- `POST /login` — authenticates and returns a bearer token
- `POST /chat` — requires authentication, returns the workflow answer
- `GET /health` — returns `200 OK` without authentication

#### Login

```bash
curl -X POST http://localhost:5000/login \
  -H "Content-Type: application/json" \
  -d '{"username": "john", "password": "password"}'
```

Response:

```json
{
  "token": "abc123..."
}
```

#### Chat (authenticated)

Use the token from login in the `Authorization` header:

```bash
curl -X POST http://localhost:5000/chat \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{"question": "What is the balance of account 10001?"}'
```

Response:

```json
{
  "output": "The balance of account 10001 is £1,532.42.",
  "executorIds": ["IntentAgent", "QueryExecutor"]
}
```

Unauthenticated requests to `/chat` return `401 Unauthorized`. The API uses the same in-memory bank seeding as the Console.

#### Per-customer authorisation

Each customer can only access their own accounts:

- **John Smith** can query accounts 10001 and 10002, but not 20001 (Jane's) or 1234567890 (Alice's)
- Transfers are allowed **from** your own accounts **to** any account (you can pay someone else)
- You cannot transfer **from** another customer's account

### Tests

```bash
dotnet test MiniBank.AI.Tests/MiniBank.AI.Tests.csproj
```

Ollama-backed tests fail immediately if Ollama is not reachable. They are not parallelized (`[Collection("Ollama")]`). `CustomerToolsTests` does not need Ollama.

## Seed data

| Account | Owner | Type | Balance |
|---|---|---|---|
| 1234567890 | Alice Example | Current | £2,450.00 |
| 10001 | John Smith | Current | £1,532.42 |
| 10002 | John Smith | Savings | £800.00 |
| 20001 | Jane Doe | Current | £5,000.00 |

Bank total: **£9,782.42**. John Smith’s combined balance: **£2,332.42**.

Owner tools do not take a customer name. They use the logged-in customer from `AuthorizedBank`. Asking about someone else still returns **your** accounts.

**Note:** After authentication, you can only query your own accounts. For example, if logged in as John Smith (username: `john`):
- `What is my total balance?` → £2,332.42 (10001 + 10002)
- `What is Jane's balance?` → still John’s total (£2,332.42); Jane’s accounts are not visible
- `Which accounts do I have?` → 10001 (£1,532.42) and 10002 (£800.00)

## Workflow

The console and workflow tests enter through `BankingWorkflow`, not a single agent with every tool.

```text
                    User
                      │
                      ▼
                Intent Agent
                      │
              ┌───────┴────────┐
              │                │
            READ             WRITE
              │                │
              ▼                ▼
       Query Executor    Approval Executor
                               │
                     ┌─────────┴─────────┐
                     │                   │
                 approved            declined
                     │                   │
                     ▼                   ▼
              Transfer Executor    Decline Executor
                     │
                     ▼
              Bank.Deposit /
              Bank.Withdraw /
              Bank.Transfer
```

```mermaid
flowchart TD
    user[User] --> intent[Intent Agent]
    intent -->|READ| query[Query Executor]
    intent -->|WRITE| approval[Approval Executor]
    query --> answer[Answer]
    approval -->|approved| transfer[Transfer Executor]
    approval -->|declined| decline[Decline Executor]
    transfer --> bank[Bank]
    transfer --> answer
    decline --> answer
```

### Why this graph exists

A single chat agent with `deposit` / `withdraw` / `transfer` tools would let the model move money as soon as it emitted a function call. Writes are therefore **not** registered on the query agent.

1. **Intent Agent** classifies the utterance. It never touches balances.
2. **READ** goes to **Query Executor**, which runs the query agent with read-only tools.
3. **WRITE** goes to **Approval Executor**, which checks structure (positive amount, account numbers, distinct transfer endpoints) and then asks `IWriteApprover`.
4. Only an approved write reaches **Transfer Executor**, which calls `OperationTools` → `Bank`.
5. A declined write goes to **Decline Executor** and never updates accounts.

`AutoApprover` is the default (approve every structurally valid write). Swap `IWriteApprover` for a human or policy check without changing the graph.

### Executors

| Id | Type | Input | Output |
|---|---|---|---|
| `IntentAgent` | LLM wrapper | user text | `BankingIntent` |
| `QueryExecutor` | LLM + READ tools | `BankingIntent` | answer string |
| `ApprovalExecutor` | code | `BankingIntent` | `ApprovalResult` |
| `TransferExecutor` | code | approved `ApprovalResult` | confirmation string |
| `DeclineExecutor` | code | rejected `ApprovalResult` | decline string |

Built in `MiniBank.AI/Workflows/BankingWorkflow.cs` with `WorkflowBuilder` + `AddSwitch`.

## Tools

### Intent (routing only)

Used only by the Intent Agent. They return a `BankingIntent`; they do not call `Bank`.

| Tool | Meaning |
|---|---|
| `classify_query` | Lookup: balances, accounts, history, totals, “how many deposits” |
| `classify_deposit` | Put money into an account now |
| `classify_withdraw` | Take money out of an account now |
| `classify_transfer` | Move money between two accounts now |

Listing deposits that already happened is a **query**, not `classify_deposit`.

### READ (query agent)

Used only by `BankingAgent` / Query Executor. These never change balances. Tools are scoped to the logged-in customer (`AuthorizedBank`); they take no owner name.

| Tool | When |
|---|---|
| `get_balance` | User supplied a specific account number they own |
| `get_owner_total_balance` | Current customer’s total, when no account number was given |
| `find_accounts_by_owner` | List the current customer’s accounts |
| `count_deposits_by_owner` | How many deposits the current customer has made |
| `get_deposits` | Deposits on one numbered account they own |
| `get_account_history` | Full history of one numbered account they own |

Implemented in `AccountTools`, `CustomerTools`, and `TransactionTools`; registered together by `QueryTools`.

### WRITE (workflow only)

`OperationTools` wraps `Bank`. The LLM never receives these. `TransferExecutor` calls them after approval.

| Method | Bank call |
|---|---|
| `deposit` | `Bank.DepositAsync` |
| `withdraw` | `Bank.WithdrawAsync` |
| `transfer` | `Bank.TransferAsync` |

## Agents

**Intent Agent** (`IntentAgent`) — temperature 0, routing tools only. Output is parsed from the function call into `BankingIntent`.

**Query Agent** (`BankingAgent`) — temperature 0, READ tools only. Used standalone in query tests, and as Query Executor inside the workflow.

Both talk to Ollama via `OllamaSharp` (`qwen2.5:1.5b-instruct`).

## Tests

Most tests use the real Ollama model, not a scripted chat client. `RecordingChatClient` records tool calls; `RecordingAccountRepository` records lookups and updates.

| Class | What it asserts |
|---|---|
| `BankingAgentTests` | Unambiguous lookups: correct READ tool, arguments, and facts in the answer |
| `BankingAgentAmbiguityTests` | Similar questions that must not pick the neighbouring tool |
| `BankingWorkflowTests` | READ skips approval/transfer; approved transfer updates balances; rejected transfer does not |
| `CustomerToolsTests` | Owner total uses the authorized customer (no LLM) |
| `AuthorizationTests` | Per-customer access control: John cannot read Jane's balance or debit 20001 (no LLM) |
| `AuthenticationTests` | Mock auth service: valid/invalid credentials, token validation (no LLM) |

Answer assertions check amounts and names, not exact LLM wording.

## Telemetry

Serilog logs agent queries, LLM responses, and tool execution. OpenTelemetry spans:

- `MiniBank.Agent` — `agent.run`, `agent.llm.chat`
- `MiniBank.Tools` — `tools.execute`
- Microsoft Agent Framework / Extensions.AI sources
- HTTP client calls to Ollama

Export is OTLP gRPC to `http://localhost:4317` when tracing is enabled.
