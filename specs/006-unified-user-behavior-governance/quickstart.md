# Quickstart: Phase 3 Implementation and Validation

Run from repository root `D:\link-dev\talabat` on branch
`feature/user-aggregate-refactor`.

## 1. Hard Preflight

```powershell
git branch --show-current
git merge-base --is-ancestor cce10d0 HEAD
git status --short
dotnet build src/Talabat/Talabat.slnx
```

Required before runtime edits:

- branch is exact;
- Phase 2 final checkpoint is an ancestor;
- the accepted Phase 3 planning artifacts have been committed;
- status is empty; and
- the solution builds.

Do not run the full suite here. The Phase 2 final checkpoint already records the preceding full
milestone, and Phase 3 final acceptance runs it again.

## 2. Incremental Validation Matrix

Run the solution build once after each grouped implementation package, followed only by the affected
project filter.

### Customer API hardening

```powershell
dotnet build src/Talabat/Talabat.slnx --no-restore
dotnet test tests/Talabat.Customer.API.Tests/Talabat.Customer.API.Tests.csproj --no-build --filter "FullyQualifiedName~OwnershipTests|FullyQualifiedName~ConcurrencyConflictEndpointTests"
```

If a frozen profile/auth contract file changes, add:

```powershell
dotnet test tests/Talabat.Customer.API.Tests/Talabat.Customer.API.Tests.csproj --no-build --filter "FullyQualifiedName~AuthEnforcementTests"
```

### Delivery assignment

```powershell
dotnet build src/Talabat/Talabat.slnx --no-restore
dotnet test tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj --no-build --filter "FullyQualifiedName~AgentAssignmentAuthorizationTests|FullyQualifiedName~DeliveryAssignmentDomainServiceTests"
```

### Drift and rowversion

```powershell
dotnet build src/Talabat/Talabat.slnx --no-restore
dotnet test tests/Talabat.Infrastructure.Tests/Talabat.Infrastructure.Tests.csproj --no-build --filter "FullyQualifiedName~CapabilityRoleDriftTests|FullyQualifiedName~ConcurrencyConflictTests"
```

### Multi-role and session invalidation

```powershell
dotnet build src/Talabat/Talabat.slnx --no-restore
dotnet test tests/Talabat.Identity.Tests/Talabat.Identity.Tests.csproj --no-build --filter "FullyQualifiedName~MultiRoleJourneyTests|FullyQualifiedName~SessionInvalidationTests"
```

Do not run another test project unless the grouped change edits its production dependency or shared
test support. Do not run the entire solution suite between these groups.

## 3. Documentation-Only Validation

No build or tests are required for superseded notes and Markdown rewrites.

```powershell
rg -n "superseded|unified user|User\.Id|ProfileNotCreated" specs/003-customer-api docs/authorization-matrix.md phase-7-architecture-guide.md
git diff --check
```

Verify every relative link resolves and historical contradictory text is under a prominent
superseded marker.

## 4. Final Full Gate — Run Once

```powershell
dotnet restore src/Talabat/Talabat.slnx
dotnet build src/Talabat/Talabat.slnx --no-restore
dotnet test src/Talabat/Talabat.slnx --no-build
dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive
dotnet ef migrations list --project src/Talabat/Talabat.Infrastructure --startup-project src/Talabat/Talabat.API
dotnet ef migrations has-pending-model-changes --project src/Talabat/Talabat.Infrastructure --startup-project src/Talabat/Talabat.API
dotnet list src/Talabat/Talabat.Domain package
```

Required:

- build succeeds;
- all four test projects pass;
- vulnerability report has no known vulnerabilities;
- migrations list contains exactly `20260719103927_InitialUnifiedUser`;
- no pending model changes;
- Domain has only `Microsoft.Extensions.Identity.Stores` 10.0.9.

## 5. Structural Sweeps

### Removed production symbols

```powershell
rg -n "ApplicationUser|ICustomerRepository|IDeliveryAgentRepository|CustomerRepository|DeliveryAgentRepository|IdentityUserId|IdentityDbContext<ApplicationUser|class\s+Customer\b|class\s+DeliveryAgent\b" src/Talabat -g "!**/obj/**" -g "!**/Persistence/Migrations/**"
```

Expected: zero hits. `CustomerId`, `AssignedAgentId`, `CustomerProfile`, and
`DeliveryAgentStatus` are intentionally retained and are not violations.

### Role/capability mutation ownership

```powershell
rg -n "AddToRoleAsync|RemoveFromRoleAsync|AddToRolesAsync|RemoveFromRolesAsync|UserType\s*[|&^]?=" src/Talabat -g "*.cs" -g "!**/obj/**"
```

Review every hit. Role membership mutations may occur only in `UserCapabilityService`; aggregate
methods may mutate their own `UserType` state but no controller/handler may do so.

### Owner-scoped input

```powershell
rg -n -i "FromRoute.*customerId|FromBody.*CustomerId" src/Talabat/Talabat.API/Controllers
rg -n -i "customerId" src/Talabat/Talabat.API/Contracts
```

Expected: no request ownership input. Response DTO fields named `CustomerId` are allowed.

### Active documentation contradictions

```powershell
rg -n -i "MUST NOT inherit|IdentityUserId|ApplicationUser|separate Customer|separate DeliveryAgent" .specify docs AGENTS.md phase-7-architecture-guide.md
```

Review hits rather than deleting history blindly. No hit may be active guidance. Historical files
may retain old text only below an explicit superseded/repealed notice. Constitution sync-report text
that states a rule was repealed is not an active contradiction.

### Final worktree integrity

```powershell
git diff --check
git status --short
```

Review the intended Phase 3 artifact set, record final evidence, and create the authorized checkpoint
before declaring the three-phase refactor complete.
