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
* **Controllers:** HTTP request handling and response delegation (`BranchesController`, `ProductsController`).
* **Contracts/Requests & Responses:** API DTOs separated from persistence models (`BranchResponse`, `ProductResponse`, `StockItemResponse`, `BranchStockResponse`).
* **Services:** Application operations and query abstractions (`IInventoryQueryService`, `InventoryQueryService`).
* **Models:** Entity Framework persistence entities (`Branch`, `Product`, `Stock`, `Transfer`).
* **Data:** `AppDbContext`, Fluent API configurations, and `DatabaseInitializer`.

---

## 📡 Available API Endpoints (Phase 2 Read APIs)

All endpoints use root routing (no `/api` prefix):

| Method | Endpoint | Description | Status Codes |
| :--- | :--- | :--- | :--- |
| `GET` | `/branches` | Returns all branches ordered by ID. | `200 OK` |
| `GET` | `/products` | Returns all products ordered by ID. | `200 OK` |
| `GET` | `/branches/{id}/stock` | Returns current stock levels for the specified branch ID. | `200 OK`, `404 Not Found` |

### Sample Response: `GET /branches/1/stock` (`200 OK`)
```json
{
  "branchId": 1,
  "branchName": "Colombo",
  "items": [
    {
      "productId": 1,
      "productName": "Rice",
      "packageSize": "1 kg bag",
      "quantity": 50
    },
    {
      "productId": 2,
      "productName": "Sugar",
      "packageSize": "1 kg bag",
      "quantity": 40
    },
    {
      "productId": 3,
      "productName": "Milk",
      "packageSize": "1 L carton",
      "quantity": 30
    },
    {
      "productId": 4,
      "productName": "Tea",
      "packageSize": "200 g packet",
      "quantity": 20
    },
    {
      "productId": 5,
      "productName": "Biscuits",
      "packageSize": "100 g packet",
      "quantity": 60
    }
  ]
}
```

### Sample Response: `GET /branches/999/stock` (`404 Not Found`)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Branch not found",
  "status": 404,
  "detail": "Branch with ID 999 does not exist.",
  "traceId": "00-..."
}
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
