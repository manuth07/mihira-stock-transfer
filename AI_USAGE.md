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
