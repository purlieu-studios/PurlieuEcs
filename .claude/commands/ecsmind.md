Goal
Answer any ECS architecture or implementation question using a small panel of specialist roles that debate in rounds, attack ideas, and finish with a single decision + concrete deliverables. Be concise and actionable.

Params
- question (required, string): The decision you want (one clear sentence).
- rounds (optional, int, default=3): Number of debate rounds.
- web (optional, on|off, default=off): If on, consult 2–4 reputable sources and cite briefly.
- scope (optional, string, default=src/PurlieuEcs): Root folder for local reasoning.
- weights (optional, string): Priorities in `key=value` pairs, e.g. `determinism=3,testability=3,performance=3,delivery=2,complexity=1,dx=2`.
- mode (optional, lite|full, default=full): Controls deliverable detail.  
- diffs (optional, on|off, default=on): Include patch suggestions if obvious.  
- code_context (optional, list of files): Narrow scan to relevant C# files.
- include_ids (optional, on|off, default=off): When on, output must include a `decision_ref` for use in patch steps.

Baseline
- Systems are engine-agnostic and stateless; no DI in hot paths.
- Components are `struct`s only (no engine refs, no heap-only collections).
- Storage is archetype+chunk SoA; queries are zero-alloc after construction.
- No reflection in hot paths; only at init/codegen.
- Events/Intents are one-frame and cleared after processing.
- Visual/engine bridges live outside the ECS assembly.

Roles
- Core Architect, API Designer, Data & Performance, Query Engineer, Test Lead, Tooling & DX, Release Manager, Integration Engineer, Red Team.

Workflow
Round 0 — Local scan
- Enumerate C# files under {scope}. Categorize: Core, Storage, Query, Systems, Events, Codegen, Snapshot, Bridges/Debug.
- Detect `*Intent` / `*Event` types and check if cleared after use.
- Flag boxing/reflection in hot paths.
- Confirm systems stateless + struct-only components.
- Summarize in ≤5 bullets.

Round 1 — Options
- Provide exactly 2 candidate answers to {question}. Max 3 sentences each.
- If web=on, include one short source note per candidate.

Round 2..N — Debate
- Each role gives one short note per round.
- Red Team explicitly attacks both against the baseline and weights.

Finalization
- Pick 1 winner OR output BLOCKED with exact files+lines to fix.
- Compute Local Fit Score 0–10 using weights; show breakdown.

Deliverables
- Decision: one paragraph.
- decision_ref: <unique ID>
- Why: 3 bullets.
- Local fit score: N/10 (with per-weight reasoning).
- Checklist: 6 next steps scoped to this week.
- Tests: 5 test names.
- Patches: 1–3 minimal diffs if `diffs=on` and mode=full.
- Risks: table (High | Medium | Low with mitigations).
- GitOps: explicit instructions for developer workflow after applying patches:
  1. Create a new branch (name = `feat/<short-decision-slug>`).
  2. Apply the patch file(s) with `git apply --3way --index`.
  3. Add new logic and generated tests (unit, integration, e2e, stress if needed).
  4. Run `dotnet build` + `dotnet test` to ensure green pipeline.
  5. Commit with message `feat(ecsmind): <short-summary>`.
  6. Push branch to origin.
  7. Open PR (using GitHub CLI if available: `gh pr create --title ... --body ...`).

Output format
Decision:
decision_ref: <unique ID>
Why:
- ...
Local fit score: N/10 (determinism=?, testability=?, performance=?, delivery=?, complexity=?, dx=?)
Checklist:
1.
2.
3.
4.
5.
6.
Tests:
- ...
- ...
- ...
- ...
- ...
Patches:
```patch
*** PATCH: path/to/file.cs
@@
- old
+ new
