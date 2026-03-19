---
goal: Build a .NET 10 multi-threading learning repository with examples and unit tests
version: 1.0
date_created: 2026-03-14
status: 'Planned'
tags: [feature]
---

# .NET Multithreading Learning Repository

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Build a structured, self-contained .NET 10 learning repository covering multi-threading from fundamentals to advanced patterns. Each topic is a self-documented static class in a class library. An xUnit test project mirrors the structure and both proves the concepts work AND serves as runnable documentation demonstrating *when* to use each primitive, *how* to use it, and *what goes wrong* without it.

---

## 1. Requirements & Constraints

- **REQ-001**: Target framework is `net10.0`; use C# 14 features where they aid clarity
- **REQ-002**: Every concept must have at least one corresponding xUnit test that proves the behavior
- **REQ-003**: Tests that demonstrate failure modes (race conditions, deadlocks) must use timeouts so they never hang the CI runner
- **REQ-004**: All public types and members must have XML doc comments explaining the concept, when to use it, and caveats
- **REQ-005**: Each source file must start with a header comment block: concept name, one-line summary, and a "When to use" / "When NOT to use" guide
- **REQ-006**: xUnit + FluentAssertions for all tests; NSubstitute is not required (no interfaces to mock)
- **REQ-007**: No Console runner project — all runnable code is exercised through tests; use `ITestOutputHelper` for output
- **CON-001**: No external NuGet dependencies beyond xUnit, FluentAssertions, and Microsoft.NET.Test.Sdk
- **CON-002**: Examples must be self-contained static classes or sealed classes; no dependency injection required
- **GUD-001**: Follow `csharp.instructions.md` — PascalCase for public members, camelCase for locals, XML docs on all public APIs
- **GUD-002**: Follow `dotnet-architecture-good-practices.instructions.md` — async/await best practices, CancellationToken end-to-end
- **GUD-003**: Test naming convention: `MethodUnderTest_Scenario_ExpectedBehavior` (from `dotnet-best-practices.instructions.md`)
- **GUD-004**: Tests follow the AAA pattern with a blank line between each section
- **PAT-001**: Deadlock and race condition demo classes must contain both a `Broken` method (exhibiting the problem) and a `Fixed` method (showing the solution), so the contrast is explicit

---

## 2. Implementation Steps

---

### Phase 1: Solution & Project Scaffold

- GOAL-001: Create the solution, both projects, all folder stubs, and verify `dotnet build` passes with zero warnings before any concept code is written.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create `DotNet.Multithreading.sln` at repository root using `dotnet new sln` | | |
| TASK-002 | Create `src/DotNet.Multithreading.Examples/DotNet.Multithreading.Examples.csproj` — `classlib`, `net10.0`, `<Nullable>enable</Nullable>`, `<LangVersion>preview</LangVersion>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` | | |
| TASK-003 | Create `tests/DotNet.Multithreading.Tests/DotNet.Multithreading.Tests.csproj` — `net10.0`, xUnit 2.x, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `FluentAssertions` (latest stable) | | |
| TASK-004 | Add both projects to the solution (`dotnet sln add`) | | |
| TASK-005 | Add project reference from Tests → Examples | | |
| TASK-006 | Create empty folder stubs in `src`: `01_Basics`, `02_ThreadPool`, `03_Tasks`, `04_AsyncAwait`, `05_Synchronization`, `06_AtomicOperations`, `07_Pitfalls`, `08_ConcurrentCollections`, `09_Patterns` (add a `.gitkeep` or a placeholder `README.md` per folder) | | |
| TASK-007 | Mirror the same folder structure in `tests/` | | |
| TASK-008 | Run `dotnet build` and confirm zero errors and zero warnings | | |
| TASK-009 | Create `README.md` at repo root with: project purpose, prerequisites, how to run tests, and a table of contents with one row per topic linking to its source folder | | |

---

### Phase 2: Thread Fundamentals

- GOAL-002: Cover the raw `Thread` API — the foundation every higher-level abstraction builds on.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-010 | Create `src/01_Basics/ThreadFundamentals.cs` — sealed static class with methods demonstrating: `new Thread(ThreadStart)`, `Thread.Start()`, `Thread.Join()`, setting `Thread.Name`, `Thread.IsBackground`, reading `Thread.ThreadState`, `Thread.CurrentThread`, `Thread.Sleep()` | | |
| TASK-011 | Create `src/01_Basics/ThreadParametersDemo.cs` — demonstrates three patterns for passing data to a thread: (a) `ParameterizedThreadStart` + cast, (b) lambda closure, (c) a dedicated state object. Includes a warning comment on closure capture bugs in loops | | |
| TASK-012 | Create `src/01_Basics/ThreadPriorityDemo.cs` — demonstrates `ThreadPriority` enum values, shows that priority is a hint not a guarantee, explains when to use it (CPU-intensive background work) and when not to (latency-sensitive work) | | |
| TASK-013 | Create `tests/01_Basics/ThreadFundamentalsTests.cs` — tests: thread completes work and Join returns; background thread dies when foreground exits (use short-lived process via Task to simulate); Name is preserved; IsBackground default is false | | |
| TASK-014 | Create `tests/01_Basics/ThreadParametersDemoTests.cs` — tests: all three parameter-passing patterns produce correct results; closure capture gotcha is demonstrated with `[Fact]` that shows wrong vs right behavior | | |
| TASK-015 | Run `dotnet test` and confirm all Phase 2 tests pass | | |

---

### Phase 3: ThreadPool

- GOAL-003: Explain when the ThreadPool is preferable to raw threads and how it manages resources.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-016 | Create `src/02_ThreadPool/ThreadPoolDemo.cs` — demonstrates: `ThreadPool.QueueUserWorkItem`, `ThreadPool.SetMinThreads` / `SetMaxThreads` / `GetMinThreads` / `GetMaxThreads`, `RegisteredWaitHandle` via `ThreadPool.RegisterWaitForSingleObject`, thread reuse (show same thread ID used for consecutive work items) | | |
| TASK-017 | Create `src/02_ThreadPool/ThreadPoolVsThreadDemo.cs` — side-by-side comparison: (a) spawning 100 raw `Thread` objects, (b) queuing 100 items to the pool. Documents memory/startup overhead difference in XML comments. Explains why ThreadPool is the right default for short-lived CPU-bound work | | |
| TASK-018 | Create `tests/02_ThreadPool/ThreadPoolDemoTests.cs` — tests: all queued work items complete (use `CountdownEvent`); min/max thread settings are respected; `RegisterWaitForSingleObject` callback fires when event is set within timeout | | |
| TASK-019 | Create `tests/02_ThreadPool/ThreadPoolVsThreadDemoTests.cs` — tests: both approaches produce identical results; test asserts ThreadPool version completes within a reasonable wall-clock ceiling | | |
| TASK-020 | Run `dotnet test` and confirm all Phase 3 tests pass | | |

---

### Phase 4: Task Parallel Library (TPL)

- GOAL-004: Cover Tasks, TaskFactory, continuations, combinators, parallel loops, and cancellation — the day-to-day tools for concurrent .NET code.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-021 | Create `src/03_Tasks/TaskBasics.cs` — demonstrates: `Task.Run`, `Task.Factory.StartNew` (with `TaskCreationOptions`), `Task<T>`, `task.Result`, `await task`, `task.ContinueWith`, `task.Status`, `TaskStatus` enum, exception handling with `AggregateException` | | |
| TASK-022 | Create `src/03_Tasks/TaskCombinators.cs` — demonstrates: `Task.WhenAll`, `Task.WhenAny`, `Task.WaitAll`, `Task.WaitAny`, `Task.WhenAll` with mixed success/failure and how to inspect individual results. Explains `WhenAll` vs `WaitAll` (async vs blocking) | | |
| TASK-023 | Create `src/03_Tasks/ParallelDemo.cs` — demonstrates: `Parallel.For`, `Parallel.ForEach`, `ParallelOptions` (MaxDegreeOfParallelism, CancellationToken), `Parallel.ForEachAsync` (.NET 6+), basic PLINQ (`.AsParallel()`, `.WithDegreeOfParallelism()`, `.WithCancellation()`, `.AsOrdered()`) | | |
| TASK-024 | Create `src/03_Tasks/CancellationDemo.cs` — demonstrates: `CancellationTokenSource`, `CancellationToken.IsCancellationRequested`, `token.ThrowIfCancellationRequested()`, `CancellationTokenSource.CancelAfter`, linked token sources (`CreateLinkedTokenSource`), cooperative vs forced cancellation | | |
| TASK-025 | Create `src/03_Tasks/TaskExceptionHandlingDemo.cs` — demonstrates: unobserved task exceptions, `AggregateException.Flatten()`, `AggregateException.Handle()`, `TaskScheduler.UnobservedTaskException`, the difference between faulted and cancelled tasks | | |
| TASK-026 | Create `tests/03_Tasks/TaskBasicsTests.cs` — tests: task returns correct value; continuation runs after antecedent; faulted task propagates exception correctly; `TaskFactory.StartNew` with `LongRunning` hint uses a dedicated thread | | |
| TASK-027 | Create `tests/03_Tasks/TaskCombinatorsTests.cs` — tests: `WhenAll` collects all results; `WhenAny` returns first completed; partial failure in `WhenAll` throws `AggregateException` with all inner exceptions; `WhenAny` does not cancel remaining tasks automatically | | |
| TASK-028 | Create `tests/03_Tasks/ParallelDemoTests.cs` — tests: `Parallel.For` result matches sequential result; cancellation stops loop early; PLINQ produces same set of results as LINQ (order-independent comparison) | | |
| TASK-029 | Create `tests/03_Tasks/CancellationDemoTests.cs` — tests: cancellation token triggers `OperationCanceledException`; `CancelAfter` fires within tolerance; linked source cancels when either parent cancels | | |
| TASK-030 | Run `dotnet test` and confirm all Phase 4 tests pass | | |

---

### Phase 5: Async/Await Patterns

- GOAL-005: Demystify async/await mechanics and document the most dangerous misuse patterns a senior developer encounters when learning async for the first time.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-031 | Create `src/04_AsyncAwait/AsyncAwaitBasics.cs` — demonstrates: async state machine concept (comment explaining what the compiler generates), `async Task`, `async Task<T>`, `ValueTask<T>` (when to use — hot path, frequently synchronous), `ConfigureAwait(false)` in library code vs application code, `await` on already-completed tasks | | |
| TASK-032 | Create `src/04_AsyncAwait/AsyncPitfalls.cs` — demonstrates (with both Broken and Fixed patterns): (a) `async void` — fire-and-forget danger, exceptions are swallowed; (b) `.Result` / `.Wait()` deadlock on a `SynchronizationContext` (illustrated with a comment reproducing the classic ASP.NET deadlock pattern); (c) loop `await` vs `WhenAll` (N sequential awaits vs one parallel await); (d) `async` method that never actually yields (returns synchronously — should use `ValueTask`) | | |
| TASK-033 | Create `src/04_AsyncAwait/AsyncStreams.cs` — demonstrates `IAsyncEnumerable<T>`, `await foreach`, `yield return` in async methods, `WithCancellation` on async streams | | |
| TASK-034 | Create `tests/04_AsyncAwait/AsyncAwaitBasicsTests.cs` — tests: `ValueTask` path that returns synchronously allocates no `Task`; `ConfigureAwait(false)` does not change result, only context | | |
| TASK-035 | Create `tests/04_AsyncAwait/AsyncPitfallsTests.cs` — tests: `async void` exception cannot be caught with try/catch (test documents this by catching `AppDomain.UnhandledException`); sequential awaits take ~N× longer than parallel `WhenAll` (measured with `Stopwatch`) | | |
| TASK-036 | Create `tests/04_AsyncAwait/AsyncStreamsTests.cs` — tests: async stream yields expected sequence; cancellation stops enumeration mid-stream | | |
| TASK-037 | Run `dotnet test` and confirm all Phase 5 tests pass | | |

---

### Phase 6: Synchronization Primitives

- GOAL-006: Cover every major .NET synchronization primitive with an honest explanation of trade-offs, overhead, and correct usage.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-038 | Create `src/05_Synchronization/LockDemo.cs` — demonstrates: `lock` keyword (compiles to `Monitor.Enter`/`Exit`), `Monitor.TryEnter` with timeout, `Monitor.Wait` / `Monitor.Pulse` / `Monitor.PulseAll` for producer-consumer signalling. XML docs: explain that `lock` should never be used on `this`, public objects, or `Type` instances | | |
| TASK-039 | Create `src/05_Synchronization/MutexDemo.cs` — demonstrates: unnamed `Mutex` (thread-affine, cross-method ownership), named `Mutex` (cross-process). XML docs: explain kernel-mode cost vs `lock`; explain `WaitOne`/`ReleaseMutex`, abandoned mutex exception | | |
| TASK-040 | Create `src/05_Synchronization/SemaphoreDemo.cs` — demonstrates: `Semaphore` (kernel-mode, cross-process), `SemaphoreSlim` (user-mode, async-capable via `WaitAsync`). Throttling pattern: limit concurrent HTTP calls to N. XML docs: Semaphore vs SemaphoreSlim decision table | | |
| TASK-041 | Create `src/05_Synchronization/EventWaitHandleDemo.cs` — demonstrates: `ManualResetEvent` (stays open until manually reset), `AutoResetEvent` (auto-resets after one release), `ManualResetEventSlim` (user-mode, preferred), `CountdownEvent` (composite signal) | | |
| TASK-042 | Create `src/05_Synchronization/ReaderWriterLockDemo.cs` — demonstrates: `ReaderWriterLockSlim`, `EnterReadLock` / `EnterWriteLock` / `EnterUpgradeableReadLock`, the upgrade pattern. XML docs: when reader-writer lock wins over plain `lock` (read-heavy workloads) | | |
| TASK-043 | Create `src/05_Synchronization/BarrierDemo.cs` — demonstrates: `Barrier` for phased parallel algorithms, `PostPhaseAction`, `ParticipantCount`, `AddParticipant` / `RemoveParticipant` | | |
| TASK-044 | Create `src/05_Synchronization/SpinDemo.cs` — demonstrates: `SpinLock` (value type — explain boxing trap), `SpinWait` for adaptive spinning. XML docs: when spinning beats blocking (extremely short critical sections on multicore) | | |
| TASK-045 | Create `tests/05_Synchronization/LockDemoTests.cs` — tests: locked counter reaches expected value under concurrent access; `Monitor.Wait`/`Pulse` correctly signals waiting thread | | |
| TASK-046 | Create `tests/05_Synchronization/SemaphoreDemoTests.cs` — tests: `SemaphoreSlim` limits concurrency to N — assert max concurrent executions never exceeds limit using an `Interlocked` peak counter | | |
| TASK-047 | Create `tests/05_Synchronization/EventWaitHandleDemoTests.cs` — tests: `ManualResetEventSlim` unblocks all waiters; `AutoResetEvent` unblocks exactly one waiter per `Set`; `CountdownEvent` signals only when all participants signal | | |
| TASK-048 | Create `tests/05_Synchronization/ReaderWriterLockDemoTests.cs` — tests: multiple readers can hold lock simultaneously; writer gets exclusive access | | |
| TASK-049 | Create `tests/05_Synchronization/BarrierDemoTests.cs` — tests: all threads reach each phase before any proceeds; `PostPhaseAction` fires exactly once per phase | | |
| TASK-050 | Run `dotnet test` and confirm all Phase 6 tests pass | | |

---

### Phase 7: Atomic Operations & Memory Model

- GOAL-007: Show exactly what `volatile`, `Interlocked`, and memory barriers do and why they matter — with tests that would *break* without them.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-051 | Create `src/06_AtomicOperations/VolatileDemo.cs` — demonstrates: `volatile` keyword guarantees visibility (no stale cache reads), does NOT guarantee atomicity for compound operations, `Volatile.Read` / `Volatile.Write` as explicit alternatives. XML docs: volatile is a lightweight memory fence for a single variable; use `Interlocked` when you need atomic read-modify-write | | |
| TASK-052 | Create `src/06_AtomicOperations/InterlockedDemo.cs` — demonstrates every `Interlocked` method: `Increment`, `Decrement`, `Add`, `Exchange`, `CompareExchange` (lock-free counter, lock-free stack push), `And`, `Or` (.NET 5+). XML docs: explain compare-and-swap (CAS) loop pattern | | |
| TASK-053 | Create `src/06_AtomicOperations/MemoryBarrierDemo.cs` — demonstrates: `Thread.MemoryBarrier()`, `Volatile.Read` / `Volatile.Write` for explicit fencing. XML docs: explain acquire/release semantics; explain why the .NET memory model is stronger than the ECMA spec on x86/x64 but not on ARM | | |
| TASK-054 | Create `tests/06_AtomicOperations/InterlockedDemoTests.cs` — tests: `Interlocked.Increment` counter matches expected value under 10,000 concurrent increments (test would fail non-deterministically without Interlocked); `CompareExchange` only swaps when current matches expected; lock-free stack push produces correct count | | |
| TASK-055 | Create `tests/06_AtomicOperations/VolatileDemoTests.cs` — tests: documents (with a comment) why the infinite-loop test requires `volatile` on the flag; asserts that the volatile-flagged version terminates within a timeout | | |
| TASK-056 | Run `dotnet test` and confirm all Phase 7 tests pass | | |

---

### Phase 8: Pitfalls & Anti-Patterns

- GOAL-008: Make common concurrency bugs visible, reproducible, and understandable — each class shows the broken version and the fixed version side by side, and the tests prove which is which.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-057 | Create `src/07_Pitfalls/RaceConditionDemo.cs` — `Broken` method: multiple threads increment a plain `int` counter (no sync); `Fixed` method: same but with `Interlocked.Increment`. XML docs: define race condition, explain why it is non-deterministic, explain why it is hard to reproduce in DEBUG vs RELEASE | | |
| TASK-058 | Create `src/07_Pitfalls/DeadlockDemo.cs` — `Broken` method: classic AB-BA lock ordering deadlock (Thread 1 acquires lockA then lockB; Thread 2 acquires lockB then lockA). `Fixed` method: consistent lock ordering. Also demonstrate `Monitor.TryEnter` with timeout as a detection escape hatch. XML docs: 4 conditions for deadlock (Coffman conditions), prevention strategies | | |
| TASK-059 | Create `src/07_Pitfalls/LivelockDemo.cs` — simulate two threads that each yield to the other indefinitely (polite-retry pattern gone wrong). `Fixed` method uses randomized back-off. XML docs: define livelock, distinguish from deadlock | | |
| TASK-060 | Create `src/07_Pitfalls/ThreadStarvationDemo.cs` — demonstrates priority inversion: a high-priority thread that can never acquire a lock held by a low-priority thread that keeps getting pre-empted. XML docs: explain starvation, explain `ReaderWriterLockSlim` writer starvation scenario | | |
| TASK-061 | Create `src/07_Pitfalls/ForgottenSynchronizationContextDemo.cs` — demonstrates the `.Result`/`.Wait()` deadlock on a captured `SynchronizationContext` via a minimal fake context implementation. XML docs: explains why this is endemic in legacy ASP.NET and WinForms code | | |
| TASK-062 | Create `tests/07_Pitfalls/RaceConditionDemoTests.cs` — tests: `Broken` — run 100 times in a loop; assert that at *least one* run produces an incorrect count OR assert that the final count is statistically unlikely to be correct (documents non-determinism with `[Fact(Skip = "...")]` and a comment). `Fixed` — assert correct count every time across 100 runs | | |
| TASK-063 | Create `tests/07_Pitfalls/DeadlockDemoTests.cs` — tests: `Broken` — wrap execution in `Task.WaitAsync(TimeSpan.FromSeconds(2))` and assert it times out (timeout = deadlock detected). `Fixed` — assert completes within 2 seconds | | |
| TASK-064 | Create `tests/07_Pitfalls/LivelockDemoTests.cs` — same timeout pattern: `Broken` times out; `Fixed` completes | | |
| TASK-065 | Run `dotnet test` and confirm all Phase 8 tests pass | | |

---

### Phase 9: Concurrent Collections

- GOAL-009: Show the thread-safe collection types, when each is appropriate, and how they compare to manually locking generic collections.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-066 | Create `src/08_ConcurrentCollections/ConcurrentDictionaryDemo.cs` — demonstrates: `GetOrAdd`, `AddOrUpdate`, `TryAdd`, `TryRemove`, `TryGetValue`, `TryUpdate`, atomic `GetOrAdd` with factory. XML docs: warn that `GetOrAdd(key, valueFactory)` factory may be called multiple times under contention; use `GetOrAdd(key, value)` overload when factory is expensive | | |
| TASK-067 | Create `src/08_ConcurrentCollections/ConcurrentQueueStackBagDemo.cs` — demonstrates: `ConcurrentQueue<T>` (FIFO, `TryDequeue`), `ConcurrentStack<T>` (LIFO, `TryPop`, `PushRange`), `ConcurrentBag<T>` (unordered, work-stealing, ideal when producer = consumer). XML docs: decision table for which to use | | |
| TASK-068 | Create `src/08_ConcurrentCollections/BlockingCollectionDemo.cs` — demonstrates bounded `BlockingCollection<T>` wrapping `ConcurrentQueue`: `Add`/`Take` blocking semantics, `CompleteAdding`, `GetConsumingEnumerable`, `TryTake` with timeout, multi-producer multi-consumer pattern | | |
| TASK-069 | Create `src/08_ConcurrentCollections/ChannelDemo.cs` — demonstrates `System.Threading.Channels`: `Channel.CreateBounded<T>`, `Channel.CreateUnbounded<T>`, `ChannelWriter<T>.WriteAsync`, `ChannelReader<T>.ReadAllAsync`, `BoundedChannelOptions` (FullMode: Wait vs DropOldest vs DropNewest), why Channel is preferred over `BlockingCollection` in async code | | |
| TASK-070 | Create `tests/08_ConcurrentCollections/ConcurrentDictionaryDemoTests.cs` — tests: concurrent `GetOrAdd` on the same key from 100 threads returns the same instance; `AddOrUpdate` accumulator reaches correct total | | |
| TASK-071 | Create `tests/08_ConcurrentCollections/BlockingCollectionDemoTests.cs` — tests: bounded queue blocks producer when full; consumer unblocks producer; `CompleteAdding` terminates consumer enumeration | | |
| TASK-072 | Create `tests/08_ConcurrentCollections/ChannelDemoTests.cs` — tests: all written items are read; `BoundedChannelOptions.FullMode = Wait` backpressures the writer; `ReadAllAsync` completes when writer is closed | | |
| TASK-073 | Run `dotnet test` and confirm all Phase 9 tests pass | | |

---

### Phase 10: Patterns & Recipes

- GOAL-010: Synthesize the primitives into real-world patterns that appear in production .NET code.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-074 | Create `src/09_Patterns/ProducerConsumerPattern.cs` — full producer-consumer using `Channel<T>`: multiple producers, multiple consumers, back-pressure, graceful shutdown via `CancellationToken` and `CompleteAdding`. XML docs: when this pattern applies (work queues, download pipelines, log aggregation) | | |
| TASK-075 | Create `src/09_Patterns/PipelinePattern.cs` — three-stage processing pipeline using chained `Channel<T>`: Stage 1 (read), Stage 2 (transform), Stage 3 (write). Each stage is an independent `async Task`. XML docs: relates to TPL Dataflow as a lightweight alternative | | |
| TASK-076 | Create `src/09_Patterns/AsyncLocalDemo.cs` — demonstrates `AsyncLocal<T>` for ambient context that flows through `await` (like a per-request correlation ID). Contrasts with `ThreadLocal<T>` which does NOT flow across awaits. Includes a test proving both behaviors | | |
| TASK-077 | Create `src/09_Patterns/LazyInitializationDemo.cs` — demonstrates: `Lazy<T>` (thread-safe by default with `LazyThreadSafetyMode.ExecutionAndPublication`), `LazyInitializer.EnsureInitialized`, double-checked locking anti-pattern vs `Lazy<T>`. XML docs: use `Lazy<T>` for expensive singletons; explain the difference between the three `LazyThreadSafetyMode` values | | |
| TASK-078 | Create `src/09_Patterns/ThrottledParallelismPattern.cs` — demonstrates limiting concurrent operations using `SemaphoreSlim` + `Task.WhenAll` (the correct pattern for "process N items with max M concurrent"). XML docs: common mistake of using `Parallel.ForEach` for async work | | |
| TASK-079 | Create `tests/09_Patterns/ProducerConsumerPatternTests.cs` — tests: all produced items are consumed exactly once; cancellation drains in-flight items and exits cleanly | | |
| TASK-080 | Create `tests/09_Patterns/PipelinePatternTests.cs` — tests: N inputs produce N outputs with correct transformation; cancellation stops all stages | | |
| TASK-081 | Create `tests/09_Patterns/AsyncLocalDemoTests.cs` — tests: `AsyncLocal<T>` value flows into child tasks; mutation in child does not affect parent; `ThreadLocal<T>` does NOT flow across `await` | | |
| TASK-082 | Create `tests/09_Patterns/ThrottledParallelismPatternTests.cs` — tests: peak concurrency never exceeds the configured limit (measured with `Interlocked` peak counter) | | |
| TASK-083 | Run `dotnet test --verbosity normal` and confirm 100% of all tests pass across all phases | | |

---

## 3. Alternatives

- **ALT-001**: Console runner project per topic (like LinqPad scripts) — rejected because it fragments the codebase and there is no machine-verifiable proof the examples are correct; tests serve both purposes
- **ALT-002**: NUnit instead of xUnit — rejected to stay consistent with `csharp-xunit.prompt.md` and project conventions
- **ALT-003**: TPL Dataflow (`System.Threading.Tasks.Dataflow`) as a dedicated topic — deferred to a future phase; the Pipeline pattern in Phase 10 uses raw Channels which are simpler and more widely applicable
- **ALT-004**: Benchmark project (BenchmarkDotNet) for performance comparisons — valuable but out of scope for v1; ThreadPool vs Thread comparison uses wall-clock timing in tests as a lightweight substitute

---

## 4. Dependencies

- **DEP-001**: `net10.0` SDK installed on the development machine and CI runner
- **DEP-002**: `xunit` ≥ 2.9.x
- **DEP-003**: `xunit.runner.visualstudio` ≥ 2.8.x
- **DEP-004**: `Microsoft.NET.Test.Sdk` ≥ 17.x
- **DEP-005**: `FluentAssertions` ≥ 8.x (latest stable for .NET 10)
- **DEP-006**: No further external dependencies

---

## 5. Files

- **FILE-001**: `DotNet.Multithreading.sln` — solution file at repository root
- **FILE-002**: `src/DotNet.Multithreading.Examples/DotNet.Multithreading.Examples.csproj` — class library
- **FILE-003**: `tests/DotNet.Multithreading.Tests/DotNet.Multithreading.Tests.csproj` — test project
- **FILE-004**: `README.md` — repo root; table of contents, prerequisites, how to run
- **FILE-005**: `src/01_Basics/ThreadFundamentals.cs`
- **FILE-006**: `src/01_Basics/ThreadParametersDemo.cs`
- **FILE-007**: `src/01_Basics/ThreadPriorityDemo.cs`
- **FILE-008**: `src/02_ThreadPool/ThreadPoolDemo.cs`
- **FILE-009**: `src/02_ThreadPool/ThreadPoolVsThreadDemo.cs`
- **FILE-010**: `src/03_Tasks/TaskBasics.cs`
- **FILE-011**: `src/03_Tasks/TaskCombinators.cs`
- **FILE-012**: `src/03_Tasks/ParallelDemo.cs`
- **FILE-013**: `src/03_Tasks/CancellationDemo.cs`
- **FILE-014**: `src/03_Tasks/TaskExceptionHandlingDemo.cs`
- **FILE-015**: `src/04_AsyncAwait/AsyncAwaitBasics.cs`
- **FILE-016**: `src/04_AsyncAwait/AsyncPitfalls.cs`
- **FILE-017**: `src/04_AsyncAwait/AsyncStreams.cs`
- **FILE-018**: `src/05_Synchronization/LockDemo.cs`
- **FILE-019**: `src/05_Synchronization/MutexDemo.cs`
- **FILE-020**: `src/05_Synchronization/SemaphoreDemo.cs`
- **FILE-021**: `src/05_Synchronization/EventWaitHandleDemo.cs`
- **FILE-022**: `src/05_Synchronization/ReaderWriterLockDemo.cs`
- **FILE-023**: `src/05_Synchronization/BarrierDemo.cs`
- **FILE-024**: `src/05_Synchronization/SpinDemo.cs`
- **FILE-025**: `src/06_AtomicOperations/VolatileDemo.cs`
- **FILE-026**: `src/06_AtomicOperations/InterlockedDemo.cs`
- **FILE-027**: `src/06_AtomicOperations/MemoryBarrierDemo.cs`
- **FILE-028**: `src/07_Pitfalls/RaceConditionDemo.cs`
- **FILE-029**: `src/07_Pitfalls/DeadlockDemo.cs`
- **FILE-030**: `src/07_Pitfalls/LivelockDemo.cs`
- **FILE-031**: `src/07_Pitfalls/ThreadStarvationDemo.cs`
- **FILE-032**: `src/07_Pitfalls/ForgottenSynchronizationContextDemo.cs`
- **FILE-033**: `src/08_ConcurrentCollections/ConcurrentDictionaryDemo.cs`
- **FILE-034**: `src/08_ConcurrentCollections/ConcurrentQueueStackBagDemo.cs`
- **FILE-035**: `src/08_ConcurrentCollections/BlockingCollectionDemo.cs`
- **FILE-036**: `src/08_ConcurrentCollections/ChannelDemo.cs`
- **FILE-037**: `src/09_Patterns/ProducerConsumerPattern.cs`
- **FILE-038**: `src/09_Patterns/PipelinePattern.cs`
- **FILE-039**: `src/09_Patterns/AsyncLocalDemo.cs`
- **FILE-040**: `src/09_Patterns/LazyInitializationDemo.cs`
- **FILE-041**: `src/09_Patterns/ThrottledParallelismPattern.cs`
- **FILE-042**: `tests/` — mirrors `src/` with one test class per demo class

---

## 6. Testing

- **TEST-001**: Every demo class has a corresponding `*Tests.cs` in the mirrored `tests/` folder
- **TEST-002**: Deadlock and livelock tests wrap execution in `Task.WaitAsync(TimeSpan.FromSeconds(2))` — timeout is the assertion for the `Broken` variant
- **TEST-003**: Race condition tests run the broken version in a loop (≥ 100 iterations) to surface non-determinism; tests are annotated with `[Trait("Category", "Flaky")]` on the broken variant so CI can exclude it from blocking gates
- **TEST-004**: Concurrency ceiling tests use an `Interlocked`-managed peak counter initialized to 0, incremented on entry and decremented on exit; the peak is asserted to not exceed the limit
- **TEST-005**: All tests use `ITestOutputHelper` for any diagnostic output — no `Console.WriteLine`
- **TEST-006**: Tests that require multiple threads use synchronization primitives (`CountdownEvent`, `Barrier`, `SemaphoreSlim`) to set up deterministic start conditions — never `Thread.Sleep` for coordination
- **TEST-007**: After completing each phase, run `dotnet test --filter "Category!=Flaky"` and assert zero failures before beginning the next phase

---

## 7. Risks & Assumptions

- **RISK-001**: Non-deterministic failures on heavily loaded CI machines — deadlock timeout tests set a 5-second ceiling which should be sufficient on any reasonable hardware; mitigated by the timeout-based assertion pattern
- **RISK-002**: ARM64 memory ordering — some volatile/memory-barrier examples behave differently on x86/x64 vs ARM64; mitigated by adding XML doc comments calling this out explicitly and ensuring tests rely on `Volatile.Read/Write` rather than raw field access
- **RISK-003**: .NET 10 preview API changes — plan targets the .NET 10 GA release; if APIs change during implementation, update to the stable equivalent and note the change in a comment
- **RISK-004**: `async void` tests are inherently tricky to assert — mitigated by using `AppDomain.UnhandledException` event subscription in the test setup with a `TaskCompletionSource` as a signal
- **ASSUMPTION-001**: The implementing agent has .NET 10 SDK installed and `dotnet` on PATH
- **ASSUMPTION-002**: No existing source files are in the workspace (confirmed: only `.github/` scaffolding exists)
- **ASSUMPTION-003**: CI/CD pipeline is not configured yet; the plan covers only local `dotnet test` verification

---

## 8. Related Specifications / Further Reading

- [.NET Threading documentation — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/threading/)
- [System.Threading.Channels — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)
- [Task Parallel Library — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl)
- [Async/Await Best Practices — Stephen Cleary](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)
- [ECMA-335 Memory Model and Volatile semantics](https://www.ecma-international.org/publications-and-standards/standards/ecma-335/)
- `csharp.instructions.md` — C# code style rules applied throughout
- `dotnet-architecture-good-practices.instructions.md` — async/await and DDD patterns
- `dotnet-best-practices.instructions.md` — test naming, xUnit conventions
- `csharp-xunit.prompt.md` — xUnit + FluentAssertions patterns