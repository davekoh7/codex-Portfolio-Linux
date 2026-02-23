
This is important because your future Codex prompts will say: “Run these and keep fixing until green.”

---

## 3) Add `AGENTS.md` so Codex follows your rules (high leverage)
Create a file named `AGENTS.md` at repo root:

```md
# Agent instructions (Codex)

Target OS: RHEL 8.10 (Linux)
Runtime: .NET 8

## Quality gates (must pass)
- dotnet restore
- dotnet build
- dotnet test

## Rules
- Keep changes minimal; avoid refactors unless requested.
- Add/adjust automated tests for any new behavior.
- Final response must include: summary, files changed, commands run and pass status.
