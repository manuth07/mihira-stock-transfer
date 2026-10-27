# Mihira Stock Transfer System

A full-stack stock transfer management solution for **Mihira Stores**, built with an ASP.NET Core Web API backend, EF Core 10, SQLite, and a React (Vite) frontend.

---

## 📋 Prerequisites & Tooling

Based on the actual project configuration:
* **.NET SDK:** `.NET 10.0` (v10.0.301 or compatible)
* **Node.js:** `v22.23.3` (or Node 20+ LTS) & `npm`

---

## 🏗️ Architecture & Technology Stack

The project follows a layered architecture within a single API project:
* **Controllers:** HTTP request handling, response mapping, and status code negotiation (`BranchesController`, `ProductsController`, `TransfersController`).
* **Contracts/Requests & Responses:** API DTOs separated from persistence models (`CreateTransferRequest`, `TransferResponse`, `BranchResponse`, `ProductResponse`, `BranchStockResponse`).
* **Services:** Application operations and transfer validation rules (`IInventoryQueryService`, `ITransferService`).
* **Models:** Entity Framework persistence entities (`Branch`, `Product`, `Stock`, `Transfer`).
* **Data:** `AppDbContext`, Fluent API configurations, and `DatabaseInitializer`.

---

## 📡 Available API Endpoints

All endpoints use root routing (no `/api` prefix):

| Method | Endpoint | Description | Status Codes |
| :--- | :--- | :--- | :--- |
| `GET` | `/branches` | Returns all branches ordered by ID. | `200 OK` |
| `GET` | `/products` | Returns all products ordered by ID. | `200 OK` |
| `GET` | `/branches/{id}/stock` | Returns current stock levels for the specified branch ID. | `200 OK`, `404 Not Found` |
| `POST` | `/transfers` | Executes an immediate stock transfer between two branches. | `200 OK`, `400 Bad Request`, `404 Not Found`, `409 Conflict`, `503 Service Unavailable` |
| `GET` | `/transfers` | Retrieves all successful transfer records (newest first). | `200 OK` |

---

## 🔄 Stock Transfer Mechanics & Rules (Phase 3)

### Validation Rules & Status Codes (`POST /transfers`)
1. **Quantity Greater Than Zero (`400 Bad Request`):** Transfer quantity must be a positive whole number (`Quantity > 0`).
2. **Distinct Branches (`400 Bad Request`):** Source and destination branches cannot be the same (`FromBranchId != ToBranchId`).
3. **Resource Existence (`404 Not Found`):** Source branch, destination branch, and product must all exist.
4. **Stock Availability & Integrity (`409 Conflict`):**
   * Source stock must be sufficient (`Available >= Requested`).
   * Source and destination stock records must exist.
   * If source stock is insufficient or concurrent modification reduces available balance, the request is rejected with `409 Conflict`.
5. **Database Contention (`503 Service Unavailable`):**
   * If SQLite reports `SQLITE_BUSY` (code 5) or `SQLITE_LOCKED` (code 6) exceeding timeout, the transaction safely rolls back and returns HTTP 503.

### Transaction Integrity & Concurrency Protection
* **Explicit SQLite Transaction:** Every transfer executes within an explicit `BeginTransactionAsync` transaction scope.
* **Atomic Conditional Deduction:** Source deduction uses `ExecuteUpdateAsync` with an atomic SQL predicate `WHERE BranchId = @from AND ProductId = @prod AND Quantity >= @qty`. If available balance dropped concurrently, 0 rows are affected, triggering immediate rollback.
* **Conservation of Inventory:** Total units of a product across all branches remain unchanged. Destination increment and transfer audit records commit in the same transaction.
* **Clean Rejection:** Rejected attempts leave all stock levels and transfer histories completely untouched. Rejected attempts are never persisted.
* **No Automatic Client Retries:** A lost or dropped HTTP response does not indicate transfer failure. Clients must not blindly retry transfer submissions without checking branch stock, as idempotency keys are outside the scope of this phase.

---

### Sample Request: `POST /transfers` (`200 OK`)
```json
{
  "fromBranchId": 1,
  "toBranchId": 2,
  "productId": 1,
  "quantity": 5
}
```

### Sample Response: `POST /transfers` (`200 OK`)
```json
{
  "id": 1,
  "fromBranchId": 1,
  "fromBranchName": "Colombo",
  "toBranchId": 2,
  "toBranchName": "Kandy",
  "productId": 1,
  "productName": "Rice",
  "packageSize": "1 kg bag",
  "quantity": 5,
  "createdAtUtc": "2026-10-27T15:29:08.4091612Z"
}
```

### Sample Response: `POST /transfers` (`409 Conflict`)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Inventory conflict",
  "status": 409,
  "detail": "Insufficient stock for product 'Rice' at branch 'Colombo'. Available: 45, requested: 9999.",
  "traceId": "00-..."
}
```

### Sample Response: `GET /transfers` (`200 OK`)
```json
[
  {
    "id": 1,
    "fromBranchId": 1,
    "fromBranchName": "Colombo",
    "toBranchId": 2,
    "toBranchName": "Kandy",
    "productId": 1,
    "productName": "Rice",
    "packageSize": "1 kg bag",
    "quantity": 5,
    "createdAtUtc": "2026-10-27T15:29:08.4091612Z"
  }
]
```

---

## 🗄️ Database & Automatic Initialization

### Serverless SQLite Setup
* The database engine is **SQLite**, which runs embedded in-process and requires **no separate database server installation**.
* **Database File Location:** `backend/MihiraStockTransfer.Api/mihira.db`. The relative `Data Source=mihira.db` connection string is resolved dynamically against the API's `ContentRootPath` via `SqliteConnectionStringBuilder`, ensuring the database path does not depend on the terminal working directory.

### Startup Seeding & Idempotency
* During application startup, `DatabaseInitializer` creates a scoped dependency injection context and calls `EnsureCreatedAsync` followed by seed checks before the web server begins accepting HTTP requests.
* **Idempotency Guarantee:** Seeding checks for existing branch IDs, product IDs, and stock composite keys `(BranchId, ProductId)`. Missing entities are inserted, while existing records and modified stock quantities are **never overwritten**.
* **Data Preservation:** Application restarts preserve existing data and transfer histories.
* **Schema Evolution Note:** `EnsureCreatedAsync` is used for this assignment to initialize tables directly without migration overhead. It does not alter existing database schemas if entity classes change. Introducing **EF Core Migrations** (`dotnet ef migrations add`) is recommended as a future enhancement for production workflows.

---

## 🏢 Business Model Reference Data

### 1. Fixed Branches
| ID | Branch Name |
| :---: | :--- |
| `1` | Colombo |
| `2` | Kandy |
| `3` | Galle |

### 2. Fixed Products
All items are stored and transferred as packaged units. Quantities represent whole-number counts of packages (no unit conversion required).

| ID | Product Name | Package Size |
| :---: | :--- | :--- |
| `1` | Rice | 1 kg bag |
| `2` | Sugar | 1 kg bag |
| `3` | Milk | 1 L carton |
| `4` | Tea | 200 g packet |
| `5` | Biscuits | 100 g packet |

### 3. Initial Stock Matrix (15 Combinations)
| Branch | Rice (1) | Sugar (2) | Milk (3) | Tea (4) | Biscuits (5) |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Colombo (1)** | 50 | 40 | 30 | 20 | 60 |
| **Kandy (2)** | 20 | 25 | 15 | 10 | 30 |
| **Galle (3)** | 15 | 20 | 20 | 15 | 25 |

---

## 🧪 Running the Backend & Tests

### Run Backend API
```bash
dotnet run --project backend/MihiraStockTransfer.Api/MihiraStockTransfer.Api.csproj
```
API runs on `http://localhost:5197` (and `https://localhost:7229`).

### Run Backend Tests
```bash
dotnet test backend/MihiraStockTransfer.Api.Tests/MihiraStockTransfer.Api.Tests.csproj
```

### Manual Testing with `.http` File
Use `backend/MihiraStockTransfer.Api/MihiraStockTransfer.Api.http` with the VS Code REST Client or Antigravity IDE HTTP runner.
