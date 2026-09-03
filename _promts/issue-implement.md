You are implementing a GitHub issue in this repository. The run was triggered by a comment on that issue.
## Goal
Read the issue title, body, labels, and the full comment thread (especially the triggering comment). Implement the requested work on a new feature branch from `main`, then open a pull request into `main`. Do not merge.
## When to stop
Do not implement if the triggering comment is not asking you to do the work (for example it is only a question, review note, or discussion). Instead comment on the issue with a short explanation and stop.
If the issue is unclear, contradictory, or missing acceptance criteria, comment on the issue with the blocking questions and stop. Do not guess a large design.
## Git
- Start from an up-to-date `main`. Never commit to `main`.
- Create a branch named `issue-<number>-<short-kebab-title>` (example: `issue-12-minibank-api-host`).
- Commit with concise messages that explain why, in this repo’s existing style.
- Do not amend pushed commits, force-push, skip hooks, or change git config.
- Do not commit secrets, `.env` files, or unrelated local files (including `_issues/` drafts unless they are already part of the requested change).
## Implementation
- Treat the issue body as the spec. The triggering comment may narrow, correct, or add constraints; follow those.
- Stay inside the stated scope and out-of-scope list. Do not drive-by refactors, extra features, or dependency upgrades.
- Match existing project conventions: .NET 10, nullable enabled, implicit usings disabled, solution file `MiniBank.AI.slnx`.
- Prefer composing existing types (for example `BankingWorkflow`) over new abstractions. Do not duplicate banking rules, agents, or tools in a new host.
- If the issue describes a thin host (console, API, or similar), copy the existing Console logging/tracing pattern: Serilog from configuration and `AddMiniBankTracing` with a distinct service name. Do not invent a parallel telemetry stack.
- Update the README only when the issue requires it (how to run, example request).
- Leave CI test jobs commented out unless the issue explicitly asks to re-enable them.
## Verify
- Run `dotnet restore MiniBank.AI.slnx` and `dotnet build MiniBank.AI.slnx --configuration Release`.
- Do not add or run Ollama-backed tests unless the issue asks for them. Ollama may not be available in this environment.
- If the build fails because of your changes, fix it before opening the PR.
## Pull request
- Push the branch and open a PR into `main` with `gh`.
- Title: conventional, specific (for example `Add MiniBank.Api Minimal API host`).
- Body must include:
  - Summary of what changed and why
  - `Closes #<issue-number>` (or `Refs #<issue-number>` if the work is partial)
  - Test plan (build command; how to run the new entry point if any)
  - Anything left undone
- After the PR exists, comment on the issue with the PR URL and a one-line status.
## Do not
- Change workflow/tool/approval behaviour unless the issue says to.
- Add auth, persistence, or extra HTTP resources unless the issue says to.
- Push to `main` or merge the PR.