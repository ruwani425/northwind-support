# Technical Approach & Architecture Decisions

This document outlines the approach, architectural choices, and thought processes across the four tasks in the Verdentra Technical Assessment for the Northwind Support Desk platform.

---

## 1. Task 1: Defect Fix (DEFECT-117) & Server-Side Filtering

### 1.1 Defect Fix (DEFECT-117)
- **Problem Analysis:** Tickets with `dueAtUtc` past their deadline that transition to `Resolved` or `Closed` were still displaying as "Breached". In the original implementation, the SLA evaluator checked elapsed time against the window without considering the terminal resolution timestamp or the ticket status.
- **Root Cause:** The domain logic evaluated SLA breach status solely based on `clock.UtcNow > ticket.DueAtUtc`, ignoring whether the ticket was already resolved within its window (`resolvedAtUtc <= dueAtUtc`).
- **Solution:** 
  - Updated domain evaluation rules: if a ticket is `Resolved` or `Closed`, the evaluation is fixed against `resolvedAtUtc`:
    - If `resolvedAtUtc <= dueAtUtc` $\rightarrow$ `Met`.
    - If `resolvedAtUtc > dueAtUtc` $\rightarrow$ `Breached`.
    - If never resolved prior to breach $\rightarrow$ `Breached`.
  - Added regression test `SlaEvaluatorTests.cs` verifying that tickets resolved prior to their due date maintain a `Met` status even when inspected well after their due date.

### 1.2 Server-Side Filtering
- **Approach:**
  - Extended EF Core LINQ query in `TicketRepository.ListAsync` to apply all search and filter predicates (`search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`) dynamically before pagination.
  - Implemented `AsNoTracking()` projections directly to `TicketListItemDto` to avoid N+1 query overhead and prevent tracking overhead on read-heavy lists.
  - Verified SQL queries using EF Core command logging to ensure `COUNT(*)` and `OFFSET ... FETCH NEXT` operate after all filters in SQL Server.

---

## 2. Task 2: Ticket Escalation, Auto-Triage & SLA Tracking

### 2.1 Domain Modeling & Aggregates
- **Ticket Entity (`Ticket.cs`):** Encapsulated ticket state transitions within the `Ticket` aggregate root rather than exposing an anemic domain model.
- **Priority Escalation (`BR-1` - `BR-4`):**
  - Priority transition is strictly monotonic: `Low` $\rightarrow$ `Medium` $\rightarrow$ `High` $\rightarrow$ `Critical`.
  - Escalation from `Critical` throws a domain rule exception (`InvalidOperationException` / HTTP 409 Conflict).
  - Escalating a `Resolved` or `Closed` ticket is prohibited.
  - Recalculates `DueAtUtc` dynamically based on the customer tier multiplier (Premium gets 50% window reduction, minimum 1 hour).
  - Emits and stores `TicketEscalation` audit history recording `FromPriority`, `ToPriority`, `FromAgentId`, `ToAgentId`, `FromDueAtUtc`, `ToDueAtUtc`, `Reason`, and `EscalatedBy`.

### 2.2 Auto-Triage & Specialist Assignment (`BR-5` - `BR-8`)
- **Triage Evaluator (`SlaEvaluator` / `TriageService`):**
  - If ticket category requires specialist or forces Critical priority, sets `Critical` priority and appropriate SLA window.
  - Auto-assignment selects the eligible agent qualified for the ticket category with the fewest active tickets.
  - If all qualified agents have reached their `MaxOpenTickets` limit, the ticket remains unassigned with clear triage explanation rather than failing creation.

### 2.3 Frontend UI Integration
- Single reusable `SlaIndicator` component with accessible ARIA tags, color tokens, and relative time formatting.
- `EscalationPanel` integrated into `TicketDetailPage` with inline confirmation, validation, and real-time status updates without full page reloads.

---

## 3. Task 3: Agent Authentication & Session Security

### 3.1 Backend Security Architecture
- **Credential Storage:** `PasswordHash` added to `Agent` entity with EF Core migration `AddAgentPasswordHash`.
- **Cryptographic Hashing:** Implemented `PasswordHasher` using RFC 2898 PBKDF2 with HMAC-SHA256, 100,000 iterations, and a 128-bit cryptographically secure random salt. Constant-time comparison prevents timing attacks.
- **JWT Issuance:** HMAC-SHA256 signed token with 8-hour lifetime containing claims: `NameIdentifier` (ID), `Email`, `Name`, and `Role`.
- **Uniform 401 Responses:** Both non-existent emails, inactive agents, and invalid passwords return identical 401 responses with `"Invalid email or password."` to eliminate agent enumeration vulnerabilities.
- **Central Authorization:** Applied ASP.NET Core fallback authorization policy (`RequireAuthenticatedUser()`), keeping only `POST /api/auth/login`, `GET /health`, and Swagger endpoints anonymous.
- **Actor Provenance:** In ticket escalation, `escalatedBy` is extracted directly from the verified JWT claims rather than trusting client payloads.

### 3.2 Frontend Auth Experience
- Built clean `/login` route with field-level validation and error banners.
- Created `ProtectedRoute` with query-preserved redirect (`/login?redirect=...`) so agents land directly on their intended ticket upon login.
- Attached Bearer tokens transparently in `client.ts` fetch wrapper and dispatched automatic session clear and redirection on 401 responses.
- Application header displays agent full name, role badge, and instant logout.

---

## 4. Task 4: API Dockerisation (Production-Ready Container)

### 4.1 Multi-Stage Build & Layer Caching
- **Stage 1 (Build):** Uses `mcr.microsoft.com/dotnet/sdk:10.0`.
  - Solution file, props, and `.csproj` files are copied first.
  - `dotnet restore` runs before copying source code, guaranteeing that dependency restoration is 100% cached when only code changes.
- **Stage 2 (Runtime):** Uses minimal `mcr.microsoft.com/dotnet/aspnet:10.0` (compressed content size ~125MB).
- **Least-Privilege Security:** Runs under built-in non-root user `app` (UID 1654) on unprivileged HTTP port `8080`.
- **Zero Secrets in Image:** Connection string, JWT signing keys, and environment variables are injected purely at runtime via environment variables or Docker Compose.
- **Health Monitoring:** Implemented `GET /health` with `DatabaseHealthCheck` verifying SQL Server readiness; Compose depends on SQL Server healthcheck before booting the API container.
