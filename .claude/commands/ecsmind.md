# /project:ecs-mind — Spec (with `phase`)

**Goal**  
Answer any ECS architecture or implementation question using a small panel of specialist roles that debate in rounds, attack ideas, and end with a single decision + concrete deliverables. Be concise and actionable.

## Params
- **question** *(required, string)*: One clear sentence decision.
- **phase** *(optional, `decide|patch|roadmap`, default=`decide`)*  
  - `decide`  → propose & choose (no code).  
  - `patch`   → produce surgical diffs for specific files (requires `decision_ref` + `files`).  
  - `roadmap` → pick the next milestone and a weekly plan (no diffs).
- **rounds** *(optional, int, default=3)*
- **web** *(optional, `on|off`, default=`off`)*: If on, consult 2–4 reputable sources and cite briefly.
- **scope** *(optional, string, default=`src/PurlieuEcs`)*
- **weights** *(optional, string)*: `determinism=3,testability=3,performance=3,delivery=2,complexity=1,dx=2`
- **mode** *(optional, `lite|full`, default=`full`)*: Output detail level (verbosity only; does **not** change behavior).
- **diffs** *(optional, `on|off`, default=`on`)*: Only relevant when `phase=patch`.
- **code_context** *(optional, list of files)*: Narrow scan to relevant C# files.
- **decision_ref** *(required when `phase=patch`)*: Short ID/slug of the prior decision (e.g., `2025-09-08-with-without-masks`).
- **files** *(required when `phase=patch`)*: Exact file paths that may be edited.
- **max_lines** *(optional, int, default=20; `phase=patch` only)*: Hard cap on changed lines across all diffs.

## Baseline (must hold; else BLOCKED)
- Systems are engine-agnostic and stateless; no DI in hot paths.
- Components are `struct`s only (no engine refs, no heap-only collections).
- Storage is archetype + chunk SoA; queries are zero-alloc after construction.
- No reflection in hot paths; only init/codegen.
- Events/Intents are one-frame and cleared after processing.
- Visual/engine bridges live **outside** the ECS assembly.

## Roles
Core Architect · API Designer · Data & Performance · Query Engineer · Test Lead · Tooling & DX · Release Manager · Integration Engineer · **Red Team**.

## Workflow

### Round 0 — Local scan
- Enumerate C# files under `{scope}`. Categorize: Core, Storage, Query, Systems, Events, Codegen, Snapshot, Bridges/Debug.
- Detect `*Intent` / `*Event` types and check one-frame clearing.
- Flag boxing/reflection in public APIs/hot paths.
- Confirm stateless systems & struct-only components (no engine refs).
- Output ≤5 bullets.

### Round 1 — Options
- Provide **exactly 2** candidate answers to `{question}` (≤3 sentences each).
- If `web=on`, include one short source note per candidate.

### Round 2..N — Debate
- Each role: one short note per round (trade-offs, risks, concrete code impacts).
- Red Team attacks both options vs baseline & weights.

## Finalization (phase-aware)
- Pick **ONE** winner **or** output `BLOCKED` with exact files+lines to fix.
- Compute **Local Fit Score 0–10** using `weights`; show per-weight rationale.
- **If `phase=decide`**: output Decision/Why/Score/Checklist/Tests/Risks. **No diffs.** Ignore `diffs:on`.
- **If `phase=patch`**: output **unified diffs only** for `files`, total changed lines ≤ `max_lines`. Also list test names.  
  - If limits would be exceeded or extra files are required → `BLOCKED(Scope)` with a stepwise plan + tests.  
  - If baseline would be violated → `BLOCKED(Baseline)` with exact lines.  
  - If the prompt is unclear/missing params → `BLOCKED(Input)`.
- **If `phase=roadmap`**: choose next milestone + weekly 6-step plan + 5 tests + risks. **No diffs.**

## Deliverables

### For `phase=decide`
- **Decision**: one paragraph.
- **Why**: 3 bullets.
- **Local fit score**: N/10 with breakdown.
- **Checklist**: 6 next steps scoped to this week.
- **Tests**: 5 test names.
- **Risks**: table (High | Medium | Low with mitigations).
- **Patches**: *not allowed* in `decide`.

### For `phase=patch`
- **Patches**: 1–3 unified diffs within `files`, total lines ≤ `max_lines`.  
- **Tests**: list new/updated test names (e.g., `IT_`, `ALLOC_`, plus category-specific).  
- **Notes**: *no prose beyond minimal context if needed*. If constraints fail → `BLOCKED(Scope|Baseline|Input)`.

### For `phase=roadmap`
- **Decision** (milestone): one paragraph.
- **Why**: 3 bullets.
- **Local fit score**: N/10 with breakdown.
- **Checklist**: 6 steps for this week.
- **Tests**: 5 test names.
- **Risks**: table.

## Output format (strict)
```
Decision:
<one paragraph>

Why:
- ...
- ...
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

Risks:
| Risk | Level | Mitigation |
|------|-------|------------|
| ...  | ...   | ...        |

Patches: (only when phase=patch; unified diffs; ≤max_lines total)
```
