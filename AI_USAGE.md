# AI Usage Record

## Phase 1: Database Foundation and Automatic Initialization

### Factual Record of AI Assistance
* **Architectural Review & Scaffolding Cleanup:**
  * Analyzed project structure, package references, and build warnings.
  * Flattened redundant nested frontend directory and unified root `.gitignore` to protect against staging .NET compilation outputs and cache files.
* **Domain Entity Models:**
  * Created `Branch`, `Product`, `Stock`, and `Transfer` entity classes under `backend/MihiraStockTransfer.Api/Models/` with navigation properties and C# nullable reference types.
* **EF Core Data Layer Configuration:**
  * Created `AppDbContext` under `backend/MihiraStockTransfer.Api/Data/` with Fluent API mappings:
    * Composite primary key `(BranchId, ProductId)` on `Stock`.
    * Restrictive delete behaviors (`DeleteBehavior.Restrict`) across reference data and transfer history.
    * Explicit navigation mappings for `Transfer.FromBranch` and `Transfer.ToBranch`.
    * Database check constraints: `CK_Stock_Quantity_NonNegative` (`Quantity >= 0`), `CK_Transfer_Quantity_Positive` (`Quantity > 0`), and `CK_Transfer_DifferentBranches` (`FromBranchId != ToBranchId`).
* **SQLite Configuration & Startup Initialization:**
  * Configured connection string in `appsettings.json` and resolved relative path against `ContentRootPath` using `SqliteConnectionStringBuilder`.
  * Created `DatabaseInitializer` with asynchronous idempotent seeding logic for 3 branches, 5 products, and 15 stock combinations saved in a single unit of work.
  * Integrated scoped database initialization during application startup in `Program.cs`.
* **Verification & Testing:**
  * Added automated tests in `DatabaseInitializationTests.cs` covering empty database seeding, idempotency on restart, preservation of modified stock quantities, database check constraint enforcement, and verification of the generated SQLite file.
  * Verified runtime execution and database creation with the .NET 10 Web API.
* **Documentation:**
  * Authored project `README.md` documenting architecture, prerequisites, database behavior, and business reference data.

---

### Personal Log (To be completed by Candidate)
* **Personal Learning:**
  * <!-- Candidate to fill in: what you learned during this phase -->
* **Time Spent:**
  * <!-- Candidate to fill in: total time spent on this phase -->
* **AI Mistakes & Corrections:**
  * <!-- Candidate to fill in: any AI missteps or corrections observed -->

## Phase 2: Read APIs for Branches, Products, and Stock

### Factual Record of AI Assistance
* **Response Contracts:**
  * Created immutable record DTOs under `backend/MihiraStockTransfer.Api/Contracts/Responses/`: `BranchResponse`, `ProductResponse`, `StockItemResponse`, and `BranchStockResponse`.
* **Query Service Layer:**
  * Defined `IInventoryQueryService` with asynchronous read-only methods (`GetBranchesAsync`, `GetProductsAsync`, `GetBranchStockAsync`).
  * Implemented `InventoryQueryService` using EF Core `AsNoTracking()` queries, direct projections to DTOs, stable ordering (`Id` / `ProductId`), and null handling for non-existent branch queries while preserving zero-quantity items.
  * Registered service as scoped in `Program.cs`.
* **API Controllers:**
  * Implemented `BranchesController` (`GET /branches`, `GET /branches/{id}/stock`) and `ProductsController` (`GET /products`) with attribute routing without `/api` prefix.
  * Configured RFC 7807 `ProblemDetails` response (404) for missing branch lookups.
  * Added accurate response-type metadata (`ProducesResponseType`).
* **Tooling & HTTP Specification:**
  * Updated `backend/MihiraStockTransfer.Api/MihiraStockTransfer.Api.http` with requests covering all three read endpoints and the 404 non-existent branch case using the development URL `http://localhost:5197`.
* **Automated & Live Verification:**
  * Added `InventoryQueryServiceTests.cs` covering stable ordering, zero-quantity inclusion, empty stock items for branches with no inventory, non-existent branch 404 ProblemDetails, and read-query non-mutation.
  * Executed live HTTP calls against the running ASP.NET Core API verifying JSON serialization, status codes, and ProblemDetails output.
* **Documentation:**
  * Updated `README.md` with available read endpoints, sample 200 OK and 404 responses, and launch/testing instructions.

---

### Personal Log (To be completed by Candidate)
* **Personal Learning:**
  * <!-- Candidate to fill in: what you learned during this phase -->
* **Time Spent:**
  * <!-- Candidate to fill in: total time spent on this phase -->
* **AI Mistakes & Corrections:**
  * <!-- Candidate to fill in: any AI missteps or corrections observed -->

## Phase 3: Stock Transfers and Transfer History

### Factual Record of AI Assistance
* **Contracts:**
  * Created `CreateTransferRequest` under `backend/MihiraStockTransfer.Api/Contracts/Requests/` with `FromBranchId`, `ToBranchId`, `ProductId`, and `Quantity`.
  * Created `TransferResponse` under `backend/MihiraStockTransfer.Api/Contracts/Responses/` containing transfer identifiers, names, package description, quantity, and UTC timestamp.
* **Service Layer & Domain Validation:**
  * Defined `ITransferService` and `TransferService` with result type `TransferResult` categorizing `InvalidRequest`, `NotFound`, `InventoryConflict`, and `DatabaseContention`.
  * Implemented business validation for positive quantity, distinct branches, resource existence, stock availability, and integer capacity.
  * Implemented explicit EF Core database transactions with atomic conditional deduction via `ExecuteUpdateAsync` (`Quantity >= requested`) and destination increment.
  * Implemented `GetTransferHistoryAsync` projecting to `TransferResponse` ordered by `CreatedAtUtc` descending and `Id` descending.
  * Added specialized exception handling for SQLite busy/locked contention codes (5 and 6) returning safe retryable outcomes.
  * Registered `ITransferService` as scoped in `Program.cs`.
* **API Controller:**
  * Implemented `TransfersController` exposing `POST /transfers` (200, 400, 404, 409, 503) and `GET /transfers` (200) with RFC 7807 `ProblemDetails` error responses and route metadata without `/api` prefix.
* **Tooling & HTTP Specification:**
  * Updated `backend/MihiraStockTransfer.Api/MihiraStockTransfer.Api.http` with requests covering valid transfers, insufficient stock, same branch, zero quantity, nonexistent product, and transfer history retrieval.
* **Automated & Live Verification:**
  * Added `TransferServiceTests.cs` covering valid transfers, full-balance transfers, validation rejections, stock conservation, history ordering, controller status code mappings, and multi-threaded concurrency using an isolated file-backed SQLite database.
  * Executed live HTTP calls against running ASP.NET Core API verifying success, error problem details, and history retrieval.
* **Documentation:**
  * Updated `README.md` with Phase 3 transfer endpoints, concurrency details, error handling rules, and retry guidance.

---

### Personal Log (To be completed by Candidate)
* **Personal Learning:**
  * <!-- Candidate to fill in: what you learned during this phase -->
* **Time Spent:**
  * <!-- Candidate to fill in: total time spent on this phase -->
* **AI Mistakes & Corrections:**
  * <!-- Candidate to fill in: any AI missteps or corrections observed -->

---

## Phase 4: Automated Test Suite for Transfer Rules and Persistence Guarantees

### Factual Record of AI Assistance
* **Test Infrastructure & Isolation:**
  * Created `SqliteTestDatabase` under `backend/MihiraStockTransfer.Api.Tests/Infrastructure/` providing isolated, temporary, file-backed SQLite database instances per test.
  * Configured connection strings with `Pooling=False` and implemented disposable/async-disposable cleanup calling `SqliteConnection.ClearAllPools()` to prevent Windows file locking issues during test teardown.
  * Created `FailingTransferSaveInterceptor` implementing `SaveChangesInterceptor` to simulate controlled database failures when saving `Transfer` entities.
* **Test Suite Implementation (`TransferServiceTests.cs`):**
  * Implemented focused integration tests covering the 10 assessment areas against real SQLite database instances:
    1. Valid transfer verifying source deduction, destination increment, cross-branch inventory conservation, receipt mapping, and server-generated UTC timestamp validity.
    2. Exact available stock transfer verifying 100% stock transfer leaves source at 0 without violating the `Quantity >= 0` check constraint.
    3. Insufficient stock rejection verifying `409 Conflict` outcome and zero database mutation.
    4. Same-branch rejection verifying `400 Bad Request` outcome and zero database mutation.
    5. Nonpositive quantity validation (zero and negative) verifying `400 Bad Request` outcome and zero database mutation.
    6. Nonexistent reference lookups (source branch, destination branch, product) verifying `404 Not Found` outcome and zero database mutation.
    7. Inventory safeguards verifying graceful conflict handling for missing source stock rows, missing destination stock rows, and destination integer capacity overflow.
    8. Transaction rollback verifying that a failure during transfer record insertion safely rolls back preceding `ExecuteUpdateAsync` deductions and increments.
    9. Concurrent spending verifying that two coordinated tasks competing for 8 units from 10 available units result in exactly one success and one failure, leaving stock at 2 (never negative), crediting 8 units, conserving total stock, and recording a single transfer.
    10. History ordering and filtering verifying empty collections for new databases, newest-first ordering, deterministic tie-breaking by `Id` descending for equal timestamps, and complete exclusion of rejected attempts.
  * Maintained HTTP controller response status code mapping tests for `TransfersController`.
* **Production Code Refinement:**
  * Refined `TransferService.cs` exception handling to catch `DbUpdateException` wrapping SQLite busy/locked codes (5 or 6) in addition to direct `SqliteException`, ensuring consistent retryable outcomes across both direct SQL and change-tracker persistence.
* **Documentation:**
  * Updated `README.md` with instructions to run all tests via `dotnet test backend/MihiraStockTransfer.slnx`, test isolation details, and business guarantee summaries.
  * Appended Phase 4 record to `AI_USAGE.md`.

---

### Personal Log (To be completed by Candidate)
* **Personal Learning:**
  * <!-- Candidate to fill in: what you learned during this phase -->
* **Time Spent:**
  * <!-- Candidate to fill in: total time spent on this phase -->
* **AI Mistakes & Corrections:**
  * <!-- Candidate to fill in: any AI missteps or corrections observed -->

---

## Phase 5: Single-Page React User Interface

### Factual Record of AI Assistance
* **Typography & Bundling:**
  * Installed `@fontsource/inter` (normal weights 400, 500, 600, 700) and imported into `main.jsx` for Vite bundle inclusion without external CDN requests.
  * Applied `Inter` font family globally across all UI elements, inputs, selects, and buttons with tabular numeral styling for quantitative columns.
* **Vite Development Proxy Configuration:**
  * Configured `vite.config.js` development proxy forwarding local `/api/*` requests to the ASP.NET Core backend at `http://localhost:5197/*` with URL rewrite.
* **API Integration Layer (`services/inventoryApi.js`):**
  * Created centralized API client module using native `fetch` covering all backend endpoints (`GET /branches`, `GET /products`, `GET /branches/{id}/stock`, `POST /transfers`, `GET /transfers`).
  * Implemented `ApiError` class distinguishing client-side network failures from explicit backend HTTP error responses.
  * Handled RFC 7807 `ProblemDetails` (`detail`/`title`), `ValidationProblemDetails` (`errors`), and non-JSON error fallbacks.
  * Added `AbortSignal` support for cancellable queries and strictly prevented automatic client retries for `POST /transfers`.
* **Component Architecture & UI Implementation:**
  * Created `AppHeader.jsx` with the exact wordmark "MIHIRA STORES" in navy on a white background with a thin bottom border.
  * Created `StockTable.jsx` rendering dynamic branch columns, product and package descriptions, right-aligned tabular numerals, empty/error/loading state boundaries, and a manual refresh action.
  * Created `TransferForm.jsx` with controlled native inputs, dynamic availability guidance ("Available: X packages · Y each"), self-transfer prevention with automatic destination reset, positive whole-number validation within `int.MaxValue`, in-flight submission disabling ("Transferring..."), explicit business rejection alerts, network failure advisories, and quantity reset upon confirmed success.
  * Created `TransferHistory.jsx` rendering audit records newest first with local date/time formatting, timezone normalization for SQLite-serialized timestamps, and distinct empty/error/loading states.
  * Created `App.jsx` coordinating root data loading, parallel requests, manual refreshes, and post-transfer updates with independent failure handling.
* **Styling & Assets:**
  * Developed custom CSS in `index.css` and `App.css` utilizing CSS custom properties for white-and-navy palettes (`#FFFFFF`, `#142B49`), restrained borders, accessible focus outlines, responsive form field stacking, and table overflow containment.
  * Removed default Vite/React starter assets and set document title to "Mihira Stores | Stock Transfers".
* **Verification & Testing:**
  * Verified zero errors or warnings via ESLint (`npm run lint`).
  * Verified successful production bundle creation via Vite (`npm run build`).
  * Verified all 37 backend tests pass via `dotnet test backend/MihiraStockTransfer.slnx`.
  * Verified live end-to-end proxy communication, stock retrieval, transfer execution, conflict rejection, and history updates.
* **Documentation:**
  * Updated `README.md` with frontend installation via `npm ci`, startup commands, proxy behavior, user flow, and font bundling details.
  * Appended Phase 5 entry to `AI_USAGE.md`.

---

### Personal Log (To be completed by Candidate)
* **Personal Learning:**
  * <!-- Candidate to fill in: what you learned during this phase -->
* **Time Spent:**
  * <!-- Candidate to fill in: total time spent on this phase -->
* **AI Mistakes & Corrections:**
  * <!-- Candidate to fill in: any AI missteps or corrections observed -->
