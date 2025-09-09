# PurlieuECS – Core Specification

This document defines the **minimal viable ECS framework** you need to ship your game logic. It is based on ArchECS, with improvements specific to your workflow, Godot integration, and game design patterns.

---

## 🎯 Goals

1. **Deterministic, pure ECS**: no engine references, stateless systems, chunked storage.  
2. **Backend–Visual Intent Pattern (BVIP)**: ECS emits intents, Godot consumes.  
3. **Zero-boxing hot path**: Span<T>, pooled iterators, no allocations per frame.  
4. **Blueprint-driven spawning**: fast declarative prefab support.  
5. **Snapshot support**: world save/load for roguelike/idle persistence.  
6. **Debug + profiling baked in**: see system timings and ECS health live.  

---

## 🗂 Project Layout

```
/src
  PurlieuEcs/
    Core/          # Entity, World, Archetype, Chunk
    Components/    # Pure structs, no engine refs
    Query/         # With/Without + chunk iterators
    Systems/       # Stateless ISystem implementations
    Scheduler/     # Ordered update, profiling
    Events/        # EventChannel<T>
    Blueprints/    # EntityBlueprint support
    Snapshot/      # Save/load world state
/tests
  PurlieuEcs.Tests/
/benchmarks
  PurlieuEcs.Benchmarks/
/game
  GodotBridge/    # VisualIntent → Godot signals
```

---

## 🧱 Core Requirements (v0)

### 1. Entities
- `Entity = (uint id, uint version)` packed into `ulong`.  
- Prevents stale references from reusing old IDs.  

### 2. Archetypes & Chunks
- Entities grouped by **signature bitset**.  
- Each chunk stores **SoA arrays** (`Span<T>`) for each component.  
- Chunk capacity: 512 by default.  

### 3. Components
- Must be `struct`.  
- No heap refs, no engine types.  
- Attributes:
  - `[Tag]` – tag component.  
  - `[OneFrame]` – auto-cleared at end of frame.  

### 4. Query
```csharp
var q = world.Query().With<Position>().Without<Stunned>();
foreach (var chunk in q.Chunks()) { ... }
```
- Fluent API with **With/Without**.  
- Zero-alloc iterators, pooled across frames.  

### 5. Systems
- `ISystem { void Update(World w, float dt); }`  
- Annotated with `[GamePhase(Update|PostUpdate|Presentation, order:int)]`.  
- Must be **stateless**.  

### 6. Scheduler
- Runs systems by phase + order.  
- Collects system timings: current, rolling average (30f), peak.  
- Provides `ResetPeaks()` for debugging.  

### 7. Events
- `EventChannel<T>`: fixed-size ring buffer.  
- `Publish(in T)` + `ConsumeAll(Action<in T>)`.  
- `[OneFrame]` events auto-cleared.  

### 8. Blueprints
- `EntityBlueprint` defines components declaratively.  
- `World.Instantiate(blueprint)` applies them directly into an archetype.  
- No reflection in hot paths (generate add-sequences).  

### 9. Snapshots
- Serialize entities, components, archetypes.  
- Header with version.  
- Compress with LZ4.  
- Restore into chunks in bulk (no per-entity alloc).  

### 10. Godot Bridge (BVIP)
- ECS never touches Godot types.  
- Systems emit `VisualIntent` only if data changes.  
- GodotBridge subscribes to intents and fires signals/animations.  

---

## 📏 Design Guardrails

- No reflection in hot paths.  
- No services, no DI containers.  
- No heap-only collections inside components.  
- Only `struct` components, `ref` access everywhere.  
- Visual updates are **change-only**.  
- All iteration is **chunk-first**, entity loops inside.  

---

## 🤖 AI Safety Rules

- **Always patch, never rewrite**: output unified diffs only, ≤20 lines. Else return `BLOCKED(Scope)`.  
- **Separation of stages**:  
  - `/project:ecs-mind` = design decision.  
  - Patch request = diff for specific file.  
- **BLOCKED categories**:  
  - `BLOCKED(Input)` → unclear question.  
  - `BLOCKED(Baseline)` → breaks ECS invariants.  
  - `BLOCKED(Scope)` → requires unsafe/large refactor.  
- **Narrow context**: specify `code_context` files, forbid edits outside.  
- **Hands-off contract**:  
  - Don’t reformat or restructure.  
  - Preserve APIs and comments.  
  - Only touch required lines.  
- **Tests with every patch**: new/updated test names must be provided.  

---

## 🧪 Testing Requirements

### Categories
- **Integration (IT_)**: cross-system, multi-frame behavior.  
- **Determinism (DET_)**: fixed seed + fixed dt ⇒ identical states & snapshot round-trips.  
- **Snapshot (SNAP_)**: save/load restores entity & component data.  
- **Performance (BENCH_)**: BenchmarkDotNet for entity creation, query iteration, migrations.  
- **Allocation (ALLOC_)**: assert zero heap allocations in hot paths.  
- **Endurance (SOAK_)**: long-run stability (spawn/destroy loops, leaks).  
- **API Analyzer (API_)**: enforce struct-only, no engine refs, no reflection in hot paths.  
- **End-to-End (E2E_)**: BVIP wiring, intents fire once, no Godot refs in ECS assembly.  

### Naming Convention
Pattern: `[Category]_[Feature]_[ExpectedBehavior]`  
Example:  
- `API_AllComponents_MustBeStructs()`  
- `ALLOC_ChunkIteration_ZeroAllocations()`  

### Regression Guardrails (CRITICAL)
1. **API Compatibility Check**: run `API_PublicSurfaceStability` + `API_BreakingChangeAnalyzer`.  
2. **Performance Validation**: `BENCH_PerformanceRegression` must show ≤50% degradation from baseline.  
3. **Test Coverage**: new features require IT_, ALLOC_, and category-specific tests.  
4. **Compilation Integrity**: all existing tests must compile and pass.  
5. **Documentation**: update API surface baseline when intentional breaking changes occur.  

**Automated Protection**
- `API_PublicSurfaceStability` – prevents accidental API breakage.  
- `BENCH_PerformanceRegression` – detects perf regressions.  
- `API_BreakingChangeAnalyzer` – validates interface stability.  
- `API_TestCoverageEnforcement` – ensures test coverage.  

---

## 🚦 Workflow Practices

### Branch and PR Requirements
- **NEVER commit directly to `main`** - all changes must go through PR review.  
- Create feature branches: `feature/entity-system`, `feature/query-builder`, etc.  
- Use descriptive branch names that match the feature being implemented.  
- Each PR should implement ONE logical feature or fix.  
- PRs must pass all tests and build successfully before merge.  
- Use squash merge to keep main branch history clean.  

### Development Workflow
- Commit after every substantial change within your feature branch.  
- Build and test the project before creating PR.  
- Update `World` when internals change.  
- Add tests for generated code where applicable.  
- After major changes, run:  
  `/project:ecs-mind "Are we still on track to a performant application?"`  
- Use import files for using statements to speed development.  

### PR Template
- **Title**: Clear, descriptive summary of changes  
- **Description**: What was implemented and why  
- **Testing**: What tests were added/updated  
- **Performance**: Any performance implications  
- **Breaking Changes**: List any API changes  

---

## 🛠 Example – Movement System (BVIP)

```csharp
public struct Position { public int X, Y; }
public struct MoveIntent { public int DX, DY; }
public struct Stunned {} // Tag

public readonly struct PositionChangedIntent {
    public Entity E; public int X, Y;
}

[GamePhase("Update", order:100)]
public sealed class MovementSystem : ISystem {
    public void Update(World w, float dt) {
        var q = w.Query().With<Position>().With<MoveIntent>().Without<Stunned>();

        foreach (var chunk in q.Chunks()) {
            var pos = chunk.GetSpan<Position>();
            var mv  = chunk.GetSpan<MoveIntent>();

            for (int i = 0; i < chunk.Count; i++) {
                var oldX = pos[i].X; var oldY = pos[i].Y;
                pos[i].X += mv[i].DX; pos[i].Y += mv[i].DY;

                if (pos[i].X != oldX || pos[i].Y != oldY) {
                    w.Events<PositionChangedIntent>().Publish(
                        new PositionChangedIntent { 
                            E = chunk.GetEntity(i), 
                            X = pos[i].X, 
                            Y = pos[i].Y 
                        }
                    );
                }
            }
        }
    }
}
```

Godot listens for `PositionChangedIntent` → moves sprite.  

---

## ✅ Definition of Done (v0)

- Entities + archetype storage working.  
- Query With/Without + chunk iteration.  
- Systems + scheduler w/ profiling.  
- EventChannel<T> + OneFrame cleanup.  
- Blueprint spawning integrated.  
- Snapshot save/load working.  
- MovementSystem + intent bridge demo in Godot.  
- Tests + benchmarks pass.  

---

## 🚦 Roadmap

- **v0 (Now)**: Entities, archetypes, queries, systems, events, scheduler, blueprints, snapshot, movement demo.  
- **v1 (Next)**: Debug overlay in Godot (timings, chunks, events).  
- **v2 (Later)**: Source-gen for archetype dispatch, fragmentation tools.  
