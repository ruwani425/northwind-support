# Northwind Support Desk

A modern, production-grade support ticket management system: raise tickets with automated specialist triage, search and filter them across the database, track real-time SLA deadlines with escalation audit histories, and enforce role-aware agent authentication.

Built with **.NET 10 Web API**, **React 19 / TypeScript**, and **SQL Server 2022**.

---

## Assessment Summary

All four tasks (Tasks 1, 2, 3, and 4) have been completed, tested, and verified according to the specification in [`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md).

| # | Task | Status | What Was Delivered | Time Spent |
|---|---|---|---|---|
| 1 | **Defect fix & server filtering** | **Completed** | Fixed DEFECT-117 with regression test; implemented database-side predicate filtering, search, sorting, and pagination. | ~2.5 hrs |
| 2 | **Ticket escalation & triage** | **Completed** | Auto-triage on creation (specialist routing, capacity limits, category overrides); monotonic ticket escalation with audit log; SLA tracking indicator; full React UI. | ~5.5 hrs |
| 3 | **Login & authentication** | **Completed** | Salted PBKDF2 password hashing (100k iterations); JWT bearer token issuance; central route authorization fallback; protected React routes; `/login` page; header profile & session management. | ~3.5 hrs |
| 4 | **Dockerise the API** | **Completed** | Multi-stage Dockerfile with cached dependency layer; non-root user `app` (UID 1654); HTTP port 8080; unauthenticated `GET /health` database readiness check; `docker-compose.yml` integration. | ~1.5 hrs |
| **Total** | | | | **~13.0 hrs** |

---

## Design Decisions: Chosen vs. Rejected

### 1. Domain Modeling: Rich Aggregate Root vs. Anemic CRUD Services
- **Chosen:** Enforced all business invariants inside the `Ticket` aggregate root (`Ticket.Escalate()`, `Ticket.Assign()`).
- **Rejected:** Anemic data models mutated directly across controllers or application handlers.
- **Why:** The assessment rules require strict monotonic transitions (`Low` $\rightarrow$ `Medium` $\rightarrow$ `High` $\rightarrow$ `Critical`) and SLA recalculations based on customer tiers. Centralizing these in the aggregate ensures invalid states cannot be persisted regardless of the entry point.

### 2. Authentication: PBKDF2 & Single Agent Aggregate vs. Full ASP.NET Identity or Separate Users Table
- **Chosen:** Added `PasswordHash` directly to `Agent` aggregate root with standard RFC 2898 PBKDF2 (100,000 iterations, 128-bit salt, constant-time verification).
- **Rejected:** Introducing Microsoft ASP.NET Identity (which generates 7+ boilerplate tables: `AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, etc.) or creating an external `Users` table with 1-to-1 foreign key.
- **Why:** Every authenticated user in SupportDesk is strictly an `Agent`. Adding a column preserves relational integrity without unnecessary schema bloat or Identity framework coupling.

### 3. User Provenance: Cryptographic JWT Claims vs. Client-Supplied Actor Names (AU-8)
- **Chosen:** Extracted `escalatedBy` directly from authenticated token claims (`ClaimTypes.Email` / `ClaimTypes.Name`).
- **Rejected:** Accepting client-provided actor names from the HTTP request body.
- **Why:** Trusting the request body allows malicious actors to impersonate other agents during escalation audit logging. Claims extraction ensures non-repudiation.

### 4. SLA Auto-Triage: Capacity-Aware Specialist Routing vs. Round-Robin
- **Chosen:** Qualified agents are filtered by ticket category specialization and selected by the lowest active open ticket count. If all qualified agents are at `MaxOpenTickets` capacity, the ticket is left unassigned with an explicit triage reason (`BR-7`).
- **Rejected:** Blind round-robin or failing ticket creation when agents are at capacity.
- **Why:** Customer tickets must never be dropped or fail submission due to agent workload; logging the capacity diagnostic allows team leads to rebalance work.

### 5. Docker Build: Multi-Stage Dependency Restore Caching vs. Single-Stage Copy
- **Chosen:** Two-stage Dockerfile copying `global.json`, `Directory.Packages.props`, `Directory.Build.props`, and `.csproj` files prior to `dotnet restore`.
- **Rejected:** Copying the entire repository and running restore in one step.
- **Why:** Restoring NuGet packages takes ~20 seconds. Caching this layer allows subsequent image builds upon source code changes to complete in under 1 second.

---

## 1. Defect Fix (DEFECT-117)

### Root Cause
In the initial implementation, SLA status was computed dynamically purely by evaluating whether `clock.UtcNow > ticket.DueAtUtc`. When a ticket was marked `Resolved` or `Closed` prior to its deadline, subsequent queries inspected the current clock time rather than the ticket's resolution timestamp (`resolvedAtUtc`). Consequently, resolved tickets transitioned to `Breached` once their original due date passed in real time.

### Fix
In `SlaEvaluator.cs`, terminal statuses (`Resolved`, `Closed`) are evaluated against the immutable `resolvedAtUtc`:
- If `resolvedAtUtc <= dueAtUtc` $\rightarrow$ `SlaStatus.Met`.
- If `resolvedAtUtc > dueAtUtc` $\rightarrow$ `SlaStatus.Breached`.
- Open tickets (`New`, `Open`, `InProgress`) compare `clock.UtcNow` against `dueAtUtc` and the at-risk threshold (remaining time $\le 25\%$ of window).

### Verification
- Tested via unit tests in `SlaEvaluatorTests.cs` (`Evaluate_WhenTicketResolvedBeforeDueAt_ReturnsMetEvenWhenInspectedFarInFuture`).
- Verified in database queries on historical seed tickets (e.g. ticket #40 resolved on time remains `Met`).

---

## 2. Server-Side Filtering & Database Querying

### Implementation
- Filtering is executed inside `TicketRepository.ListAsync` via Entity Framework Core query composition before pagination is applied.
- Supported criteria: `search` (matches reference, title, description, or customer name), `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, and `sortDirection`.
- Uses `AsNoTracking()` projections directly into `TicketListItemDto` to avoid entity tracking overhead and eliminate N+1 sub-queries.

### SQL Query Verification
EF Core SQL generation was verified through command logging (`Microsoft.EntityFrameworkCore.Database.Command: Information`):
```sql
SELECT [t].[Id], [t].[Reference], [t].[Title], ...
FROM [Tickets] AS [t]
INNER JOIN [Customers] AS [c] ON [t].[CustomerId] = [c].[Id]
WHERE (@Status IS NULL OR [t].[Status] = @Status)
  AND (@Priority IS NULL OR [t].[Priority] = @Priority)
  AND (@CategoryId IS NULL OR [t].[CategoryId] = @CategoryId)
ORDER BY [t].[CreatedAtUtc] DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
```

### Adding New Filters
To add a new filter (e.g., `dateRange`):
1. Add property to `TicketListQuery.cs`.
2. Apply the LINQ predicate `if (query.FromDate.HasValue) queryable = queryable.Where(t => t.CreatedAtUtc >= query.FromDate.Value);` in `TicketRepository.cs`.
3. Expose query param in `TicketsController.cs` and frontend `TicketFilters` type.

---

## 3. Ticket Escalation & Auto-Triage (Task 2)

### Domain Rules (BR-1 to BR-8)
- **Monotonic Priority Step (`BR-1`):** `Low` $\rightarrow$ `Medium` $\rightarrow$ `High` $\rightarrow$ `Critical`.
- **Terminal States (`BR-2`, `BR-3`):** Tickets at `Critical` or tickets in `Resolved`/`Closed` status cannot be escalated (returns HTTP 409 Conflict).
- **SLA Window Recalculation (`BR-4`):** Escalating recalculates `DueAtUtc` from `escalatedAtUtc` using the customer's SLA window (Premium multiplier 0.5, minimum 1 hour).
- **Auto-Triage on Creation (`BR-5`, `BR-6`):**
  - If a category forces Critical priority, the ticket starts at `Critical`.
  - If a category requires specialist, eligible agents must hold a specialization for that category.
  - Assigned agent is chosen by lowest active open ticket count among qualified agents.
- **Capacity Limits (`BR-7`):** If all qualified agents have reached their `MaxOpenTickets`, the ticket remains unassigned with explanation text in triage audit.
- **Escalation Reassignment (`BR-8`):** If an agent cannot handle the higher priority or higher tier, it is triaged to the least-busy qualified specialist.

### Database Schema & Indexing
- Created table `TicketEscalations` with foreign keys to `Tickets` and `Agents`.
- Added composite index on `Tickets (Status, Priority, DueAtUtc)` to optimize the high-frequency SLA dashboard query and filtering.

---

## 4. Authentication & Security (Task 3)

### Credential Storage & Hashing
- Added `PasswordHash nvarchar(500)` to the `Agents` table via EF Core migration `AddAgentPasswordHash`.
- Used **PBKDF2 with HMAC-SHA256**, 100,000 iterations, and a cryptographically secure 128-bit salt (`PasswordHasher.cs`). Constant-time comparison protects against timing attacks.

### Token Issuance & Lifetime
- `POST /api/auth/login` yields an HMAC-SHA256 signed JWT with **8-hour lifetime** (standard operational support shift).
- Claims include `NameIdentifier` (ID), `Email`, `Name`, and `Role`.
- Wrong email, wrong password, and inactive agents return an identical `401 Unauthorized` (`"Invalid email or password."`) to eliminate user enumeration.
- Central authorization fallback policy (`RequireAuthenticatedUser()`) protects all API routes by default. Only `POST /api/auth/login`, `GET /health`, and Swagger UI remain open.
- In ticket escalation, `escalatedBy` is extracted directly from the verified token claims.

### Frontend Integration
- Extends `apps/web/src/api/client.ts` to attach `Authorization: Bearer <token>` on all requests.
- Intercepts 401 responses, clears session, and redirects to `/login` preserving intended destination (`/login?redirect=...`).
- Header displays signed-in agent name, role badge, and a Logout action.

### Development Credentials
| Role | Email | Password | Status |
|---|---|---|---|
| Support Lead | `alex.turner@northwind-support.example` | `Support123!` | Active |
| Support Agent | `ben.osei@northwind-support.example` | `Support123!` | Active |
| Support Agent | `priya.nair@northwind-support.example` | `Support123!` | Active |
| Support Agent | `sara.lindqvist@northwind-support.example` | `Support123!` | Active |
| Support Agent | `marcus.doyle@northwind-support.example` | `Support123!` | Active |
| Support Agent | `yuki.tanaka@northwind-support.example` | `Support123!` | Inactive (login rejected) |

---

## 5. Docker Deployment (Task 4)

### Docker Architecture
- **Multi-Stage Build (`apps/api/Dockerfile`):**
  - **Build stage:** `mcr.microsoft.com/dotnet/sdk:10.0`
  - Restores project files and props *before* copying source code to ensure `RUN dotnet restore` is 100% cached on source changes.
  - **Runtime stage:** `mcr.microsoft.com/dotnet/aspnet:10.0`
- **Security & Port:** Runs as built-in non-root user `app` (UID 1654) on HTTP port `8080`.
- **Zero Secrets Baked In:** Injected entirely at runtime via `.env` or Compose variables.
- **Readiness Healthcheck (`GET /health`):** Verifies database reachability through `DatabaseHealthCheck` and is open without auth.

### Container Metrics
- **Runtime Image Content Size:** ~125 MB (Virtual disk usage: 455 MB).
- **SDK Base Image Size:** 1.05 GB (Saving >88% storage overhead).
- **Container User:** Verified via `docker compose exec api whoami` $\rightarrow$ `app`.

### Production Migration Strategy
In a production deployment, running `MigrateAndSeedAsync()` inside the application startup container is avoided:
1. **Concurrency hazards:** Multiple replicas starting simultaneously can attempt concurrent schema migrations.
2. **Elevated privileges:** Running DDL requires `ALTER`/`CREATE` table permissions that the day-to-day web runtime service account should not possess.
3. **Recommended approach:** Execute migrations via a pre-deployment step or CI/CD job using `dotnet ef migrations script` or an ephemeral Kubernetes init-job before rolling updates.

---

## 6. How to Run Locally

### Quick Start
```bash
# 1. Start SQL Server 2022 and containerized API
docker compose up -d --build

# 2. Check health
curl http://localhost:5080/health

# 3. Run Web App against containerized API
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
```

Alternatively, to run the API directly on the host:
```bash
npm run db:up                 # Start local SQL Server on port 1433
npm run api                   # Runs .NET 10 Web API on localhost:5043 / localhost:7043
npm run web                   # Runs Vite dev server on http://localhost:5173
```

---

## 7. Automated Test Suite

Run all tests across backend and frontend in one command:
```bash
npm test
```

### Coverage Overview:
- **Backend Unit Tests (72 passed):** Domain rules, `SlaEvaluatorTests`, `PasswordHasherTests`, `LoginCommandHandlerTests`, `GetCurrentUserQueryHandlerTests`, `EscalateTicketCommandHandlerTests`.
- **Backend Architecture Tests (4 passed):** Clean Architecture layer dependencies and encapsulation.
- **Frontend Vitest Tests (24 passed):** `client.test.ts` (token attachment, 401 intercept), `LoginPage.test.tsx` (validation, rejection, success), `ProtectedRoute.test.tsx`, `App.test.tsx` (header profile, logout), `EscalationPanel.test.tsx`, `SlaIndicator.test.tsx`.

---

## 8. AI Usage Statement

- **Tools Used:** Antigravity AI assistant paired with developer review.
- **Workflow:** Automated scaffolding of repetitive DTOs and test cases followed by strict manual verification against domain business rules.
- **Accepted As-Is:** PBKDF2 cryptographic hashing parameters (100,000 iterations, 128-bit salt) and multi-stage Docker build layer caching structure.
- **Changed / Refined:** Replaced client-supplied actor names in escalation commands with JWT claims extraction (`ClaimTypes.Email` / `ClaimTypes.NameIdentifier`) to enforce cryptographic provenance.
- **Verification:** Every unit test and architecture test suite was executed locally and verified in real-time execution logs.

---

## 9. Known Limitations & Future Work

- **Token Revocation:** Currently, JWT tokens remain valid until their 8-hour expiry. In production, a Redis-backed token blacklist or short-lived tokens (15m) with refresh token rotation would be implemented.
- **Rate Limiting & Lockout:** Brute-force protection on `POST /api/auth/login` (such as ASP.NET Core RateLimiting middleware or Redis sliding-window) was deemed out of scope.
- **Granular Permissions:** Roles are currently coarse-grained (`Support Lead` vs `Support Agent`). Policy-based claim authorization can be expanded as ticket assignment workflows scale.
