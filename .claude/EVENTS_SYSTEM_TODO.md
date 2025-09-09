# Events/Intents System Implementation TODO

## 🎯 Goal
Implement the complete Events/Intents system with single-layer BVIP pattern where ECS systems emit VisualIntent structs directly to external bridge consumers.

## 📋 Core Implementation Tasks

### Phase 1: Event Channel Infrastructure
- [ ] **EventChannel<T> class** - Ring buffer implementation with fixed capacity
  - [ ] Zero-allocation publish/consume APIs
  - [ ] Overflow handling (drop oldest or reject new)
  - [ ] Thread-safe operations for concurrent access
  - [ ] Generic type support for any intent struct

- [ ] **World event management** - Integration point for event channels  
  - [ ] `Events<T>()` method to get/create event channels
  - [ ] `ClearOneFrameEvents()` method for frame cleanup
  - [ ] Event channel registry and lifecycle management

- [ ] **OneFrame attribute** - Marker for events that auto-clear
  - [ ] `[OneFrame]` attribute class for intent types
  - [ ] Automatic detection and clearing mechanism
  - [ ] Integration with World update cycle

### Phase 2: Intent Type Definitions
- [ ] **Base intent infrastructure** - Foundation for all intent types
  - [ ] `IVisualIntent` marker interface (optional - for type safety)
  - [ ] Intent naming conventions and patterns
  - [ ] Common intent base structs if needed

- [ ] **Concrete intent types** - Specific intents for game systems
  - [ ] `PositionChangedIntent` - Entity position updates
  - [ ] `AudioTriggeredIntent` - Sound effect triggers  
  - [ ] `HealthChangedIntent` - Health/damage updates
  - [ ] `AnimationTriggeredIntent` - Animation state changes

### Phase 3: System Integration
- [ ] **Update MovementSystem** - Enable actual intent emission
  - [ ] Remove commented intent emission code
  - [ ] Add real PositionChangedIntent publishing
  - [ ] Only emit when position actually changes
  - [ ] Performance-optimized emission logic

- [ ] **Create additional example systems** - Show pattern usage
  - [ ] HealthSystem with HealthChangedIntent emission
  - [ ] AudioSystem with AudioTriggeredIntent emission
  - [ ] Demonstrate different intent patterns

### Phase 4: External Bridge Foundation
- [ ] **GodotBridge stub** - External assembly for engine integration
  - [ ] Separate assembly outside of ECS core
  - [ ] Intent consumption and processing
  - [ ] Engine-specific translation layer
  - [ ] Signal/event forwarding to Godot

- [ ] **Bridge interface design** - Clean separation of concerns
  - [ ] IBridge interface for different engines
  - [ ] Intent subscription and handling patterns
  - [ ] Batch processing for performance

## 🧪 Comprehensive Testing Tasks

### Core Event System Tests
- [ ] **API_EventChannel_PublishConsume_ShouldWorkCorrectly**
  - [ ] Basic publish/consume functionality
  - [ ] Multiple consumers on same channel
  - [ ] Event ordering preservation

- [ ] **ALLOC_EventPublishing_ShouldNotAllocateExcessively** 
  - [ ] Zero-allocation publish operations
  - [ ] Zero-allocation consume operations
  - [ ] Ring buffer reuse validation

- [ ] **PERF_EventChannel_ShouldScaleWithManyEvents**
  - [ ] Performance with high event volumes
  - [ ] Scaling behavior validation
  - [ ] Memory usage under load

### Intent Integration Tests  
- [ ] **IT_MovementSystem_ShouldEmitPositionChangedIntents**
  - [ ] Intent emission on position changes
  - [ ] No emission when position unchanged
  - [ ] Correct intent data population

- [ ] **DET_OneFrameEvents_ShouldClearAfterProcessing**
  - [ ] [OneFrame] intents cleared automatically
  - [ ] Regular intents persist across frames
  - [ ] Clear timing and ordering

### End-to-End Tests
- [ ] **E2E_EcsToGodotBridge_ShouldProcessIntentsCorrectly**
  - [ ] Complete ECS→Bridge→Engine pipeline
  - [ ] Intent translation accuracy
  - [ ] Performance of full pipeline

### Edge Case Tests
- [ ] **EDGE_EventChannelOverflow_ShouldHandleGracefully**
  - [ ] Ring buffer overflow behavior
  - [ ] Event dropping/rejection policies
  - [ ] Recovery after overflow

- [ ] **EDGE_EventChannelThreadSafety_ShouldWorkConcurrently**
  - [ ] Concurrent publish/consume operations
  - [ ] Thread safety validation
  - [ ] Race condition prevention

## 🔧 Implementation Guidelines

### Performance Requirements
- Event publishing: < 0.001ms per intent
- Event consumption: < 0.01ms per batch
- Zero heap allocations in hot paths
- Ring buffer reuse for memory efficiency

### API Design Principles  
- Fluent API: `world.Events<PositionChangedIntent>().Publish(intent)`
- Type-safe intent definitions with struct-only data
- Automatic lifetime management for [OneFrame] events
- Clear separation between ECS and engine concerns

### Testing Standards
- All new APIs require API_, ALLOC_, and IT_ test coverage
- Performance tests for scaling behavior
- Edge case coverage for error conditions
- End-to-end validation of BVIP pattern

## 📦 Deliverables Checklist
- [ ] EventChannel<T> implementation with tests
- [ ] World event management APIs with tests  
- [ ] Intent type definitions and examples
- [ ] Updated MovementSystem with real intent emission
- [ ] GodotBridge foundation with basic intent consumption
- [ ] Complete test suite (15+ tests covering all scenarios)
- [ ] Documentation updates for BVIP pattern usage
- [ ] Performance benchmarks for event system

---
*Generated: 2025-09-09*  
*Status: Ready for implementation*  
*Estimated effort: 1-2 weeks*