# AI Usage Record

This document provides a realistic, concise record of how AI assistance was used throughout the development of the Mihira Stock Transfer system.

---

## Phase 1: Database Foundation and Automatic Seeding

### What AI Was Used For
* **Entity & Data Layer Scaffolding:** Designed the core EF Core entity models (`Branch`, `Product`, `Stock`, `Transfer`) with navigation properties.
* **Database Constraints:** Configured `AppDbContext` with Fluent API mappings, including the composite primary key `(BranchId, ProductId)`, restrictive foreign key delete behaviors, and SQL check constraints (`Quantity >= 0`, `Quantity > 0`, `FromBranchId != ToBranchId`).
* **Startup Initializer:** Implemented `DatabaseInitializer` to seed the 3 fixed branches, 5 packaged products, and initial 15 stock balances idempotently on application startup.
* **Initial Test Verification:** Created unit tests in `DatabaseInitializationTests.cs` to verify database creation, seeding idempotency, and check constraints.

---

## Phase 2: Read APIs for Branches, Products, and Stock

### What AI Was Used For
* **Response Contracts:** Created immutable record DTOs (`BranchResponse`, `ProductResponse`, `StockItemResponse`, `BranchStockResponse`) to decouple internal EF Core models from API consumers.
* **Query Service:** Implemented `InventoryQueryService` using `AsNoTracking()` queries with deterministic ordering and null checks for nonexistent branches.
* **API Controllers:** Built `BranchesController` and `ProductsController` with root routing (no `/api` prefix), RFC 7807 `ProblemDetails` for 404 lookups, and OpenAPI response metadata.
* **Automated Query Testing:** Wrote tests in `InventoryQueryServiceTests.cs` verifying sorting, zero-quantity inclusion, and non-mutation of data during read operations.

---

## Phase 3: Stock Transfers and Transfer History

### What AI Was Used For
* **Contracts & Validation:** Created `CreateTransferRequest` and `TransferResponse` DTOs with validation rules (positive quantity, distinct branches, resource existence).
* **Transfer Engine:** Implemented `TransferService` with explicit database transactions and atomic conditional updates via `ExecuteUpdateAsync` (`WHERE Quantity >= requested`) to eliminate race conditions and enforce total inventory conservation.
* **Audit History:** Added `GetTransferHistoryAsync` to retrieve successful transfers ordered newest first with UTC timestamps.
* **API Controller:** Built `TransfersController` supporting `POST /transfers` and `GET /transfers` with RFC 7807 `ProblemDetails` for validation (400), not found (404), insufficient stock conflicts (409), and SQLite contention (503).

---

## Phase 4: Automated Integration Test Suite

### What AI Was Used For
* **Test Isolation Infrastructure:** Created `SqliteTestDatabase` to generate isolated, temporary file-backed SQLite databases with `Pooling=False` and pool flushing to eliminate file locks and cross-test state leakage.
* **Controlled Rollback Testing:** Built `FailingTransferSaveInterceptor` to simulate a database failure during transfer record persistence, proving that preceding `ExecuteUpdateAsync` deductions roll back completely.
* **Comprehensive Test Suite:** Wrote tests covering 10 distinct areas in `TransferServiceTests.cs`, including 100% stock exhaustion to zero, zero/negative rejection, missing references, inventory overflow safeguards, and deterministic multi-threaded concurrency using `Barrier`.
* **Production Refinement:** Added exception handling for `DbUpdateException` wrapping SQLite busy/locked codes (5 and 6) during `SaveChangesAsync`.

---

## Phase 5: Single-Page React User Interface

### What AI Was Used For
* **Component Architecture:** Built focused React components (`AppHeader`, `StockTable`, `TransferForm`, `TransferHistory`) coordinated by `App.jsx`.
* **Vite Proxy & API Layer:** Configured Vite dev proxy to route `/api/*` to the backend on port 5197, and created `inventoryApi.js` to normalize RFC 7807 errors and network connection failures.
* **Typography & Design:** Installed and bundled the `@fontsource/inter` font family locally without external CDN calls, and applied a white-and-navy aesthetic with tabular numerals for quantity columns.
* **Form UX Improvements:** Added a controlled digits-only quantity input rejecting invalid characters, integrated up/down stepper buttons, keyboard `ArrowUp`/`ArrowDown` support, and updated error/success banners with brighter, high-visibility colors.

---

### Personal Log 
* **Personal Learning:**
  * **SQLite Concurrency & Atomic Predicates:** Learned how SQLite handles write locks and how combining explicit database transactions with atomic conditional SQL updates (`ExecuteUpdateAsync` with `WHERE Quantity >= requested`) guarantees inventory conservation under concurrent requests without needing distributed lock managers.
  * **Transaction Rollback Testing with Interceptors:** Learned how to use an EF Core `SaveChangesInterceptor` (`SavingChangesAsync`) to simulate a mid-transaction database failure, proving that atomic SQL deductions roll back cleanly when subsequent entity saves fail.
  * **Test Isolation on Windows:** Learned that SQLite connection pooling (`Pooling=True`) keeps open OS file handles on Windows; configuring test databases with `Pooling=False` and flushing pools via `SqliteConnection.ClearAllPools()` is essential for reliable file deletion during test teardown.
  * **Timestamp Serialization Nuance:** Observed that SQLite stores datetime values without timezone metadata, causing EF Core to return `DateTimeKind.Unspecified` (omitting `Z` in JSON serialization on `GET /transfers`). Learned the importance of explicit client-side timezone normalization.
  * **Accessible Controlled Inputs:** Deepened understanding of building controlled numeric inputs with WAI-ARIA `spinbutton` semantics, native stepper controls, and keyboard `ArrowUp`/`ArrowDown` support.

* **Time Spent:**
  * Approximately 3 hours (7:30 PM – 10:30 PM) for development across all phases, plus ~15 minutes for documentation, ER diagram authoring, and test suite verification.

* **AI Mistakes & Corrections:**
  * **Background Process File Locking:** When verifying the running API, the background `MihiraStockTransfer.Api.exe` process remained active on Windows, locking the compiled assembly during subsequent `dotnet test` builds. Identified the lock (`MSB3026`) and terminated the lingering PID using `Stop-Process -Force`.
  * **React 19 Effect State Warning:** An early implementation of `App.jsx` triggered ESLint's `react-hooks/set-state-in-effect` rule by calling synchronous `setState(true)` at the top of an effect function. Corrected this by refactoring the initial load cleanly inside `useEffect` and reserving loading setters for user-initiated refreshes.
  * **Git Branch Synchronization:** When starting Phase 4, the workspace was still on the Phase 3 branch (`feature/stock-transfers`). Instead of improperly switching branches autonomously, the prompt constraint was flagged and the branch was manually switched to `test/stock-transfer-rules` before modifying code.
  * **Timestamp Formatting Contract:** The API serialized in-memory timestamps with `Z` on `POST /transfers`, but returned unzoned ISO strings on `GET /transfers` from SQLite. Corrected the frontend to explicitly inspect timestamps and append `Z` to prevent local-time parsing errors in JavaScript.