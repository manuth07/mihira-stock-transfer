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

