# MiniBank AI

A Microsoft Agent Framework assistant for the MiniBankDotnet banking domain. Lookups go through read-only tools. Deposits, withdrawals, and transfers cannot be invoked by the model; they run only after a workflow approval step.

This repo sits next to [`MiniBankDotnet`](../MiniBankDotnet) and references its domain and services projects.

## Layout

| Project | Role |
|---|---|
| `MiniBank.AI` | Agents, tools, workflow, telemetry |
| `MiniBank.Console` | Host + Serilog + OpenTelemetry; runs sample questions |
| `MiniBank.AI.Tests` | Query-agent tests and workflow routing tests against live Ollama |

## Prerequisites

- .NET 10
- [Ollama](https://ollama.com) at `http://localhost:11434`
- Model `qwen2.5:1.5b-instruct`

```bash
ollama pull qwen2.5:1.5b-instruct
```

Optional: an OTLP collector at `http://localhost:4317` (see `MiniBank.Console/appsettings.json`). Tracing can be turned off with `"Tracing": { "Enabled": false }`.

## Run

```bash
dotnet run --project MiniBank.Console
```

The console seeds an in-memory bank, then runs a lookup and a transfer through the workflow.

```bash
dotnet test MiniBank.AI.Tests/MiniBank.AI.Tests.csproj
```

Tests fail immediately if Ollama is not reachable. They are not parallelized (`[Collection("Ollama")]`).

## Seed data

| Account | Owner | Type | Balance |
|---|---|---|---|
| 1234567890 | Alice Example | Current | £2,450.00 |
| 10001 | John Smith | Current | £1,532.42 |
| 10002 | John Smith | Savings | £800.00 |
| 20001 | Jane Doe | Current | £5,000.00 |

Bank total: **£9,782.42**. John Smith’s combined balance: **£2,332.42**.

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

Used only by `BankingAgent` / Query Executor. These never change balances.

| Tool | When |
|---|---|
| `get_balance` | User supplied a specific account number |
| `get_owner_total_balance` | Named customer, no account number |
| `find_accounts_by_owner` | List a customer’s accounts |
| `get_total_value` | Sum of every account in the bank |
| `get_highest_balance_account` | Account with the largest balance |
| `count_deposits_by_owner` | How many deposits a customer has made |
| `get_deposits` | Deposits on one numbered account |
| `get_account_history` | Full history of one numbered account |

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

Tests use the real Ollama model, not a scripted chat client. `RecordingChatClient` records tool calls; `RecordingAccountRepository` records lookups and updates.

| Class | What it asserts |
|---|---|
| `BankingAgentTests` | Unambiguous lookups: correct READ tool, arguments, and facts in the answer |
| `BankingAgentAmbiguityTests` | Similar questions that must not pick the neighbouring tool |
| `BankingWorkflowTests` | READ skips approval/transfer; approved transfer updates balances; rejected transfer does not |

Answer assertions check amounts and names, not exact LLM wording.

## Telemetry

Serilog logs agent queries, LLM responses, and tool execution. OpenTelemetry spans:

- `MiniBank.Agent` — `agent.run`, `agent.llm.chat`
- `MiniBank.Tools` — `tools.execute`
- Microsoft Agent Framework / Extensions.AI sources
- HTTP client calls to Ollama

Export is OTLP gRPC to `http://localhost:4317` when tracing is enabled.
