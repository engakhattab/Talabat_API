# Quickstart: Phase 2 Unified User Cutover

This is the implementation/verification runbook for
[plan.md](plan.md). Run commands from `D:\link-dev\talabat` in PowerShell.

## 0. Current status: do not implement yet

At planning time Phase 1 T029/T030 is open and the tree is dirty. Phase 2 implementation must stop
until the following preflight is fully green.

```powershell
git branch --show-current
git status --short
rg -n "T029|T030" specs/004-unified-user-domain-model/tasks.md
dotnet build src/Talabat/Talabat.slnx
dotnet test src/Talabat/Talabat.slnx
dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive
```

Required:

- branch output is `feature/user-aggregate-refactor`;
- T029 and T030 are complete;
- build and all tests pass;
- vulnerability output lists no vulnerable package (including Delivery API transitives);
- Phase 1 has a recorded commit; and
- final `git status --short` output is empty.

If any condition fails, stop. Do not delete legacy types, migrations, or a database.

## 1. Execute the compile-break window

Implement WP1–WP8A and WP9A from [plan.md](plan.md) in order. The solution may not compile during
this window. Do not run the development database drop and do not scaffold the migration until the
final EF model, host foundation, and structural test migration are in place.

Critical invariants during editing:

- do not introduce `ApplicationUser`, separate Customer/DeliveryAgent aggregates, or
  `IdentityUserId`;
- do not rename `CustomerId`, `AssignedAgentId`, `DeliveryAgentStatus`, or Customer DTOs;
- do not accept any role/capability value from HTTP callers;
- do not assign DeliveryAgent flag/role/status at applicant registration;
- do not use `UserManager` in Application or Domain;
- do not add packages outside the constitution boundaries;
- do not alter the two existing anonymous `ProfileNotCreated` response objects; and
- do not implement a Delivery API or admin approval controller.

Close the compile window:

```powershell
dotnet restore src/Talabat/Talabat.slnx
dotnet build src/Talabat/Talabat.slnx --no-restore
dotnet test tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj --no-build
```

Do not proceed until all three pass.

## 2. Regenerate migration source without touching the development database

The SQL fixtures call `MigrateAsync`; they require the new initial migration before the full test
suite can pass. First verify that no EF/database process is running. Then remove only these known
source artifacts:

```powershell
$phase2MigrationFiles = @(
  'src/Talabat/Talabat.Infrastructure/Persistence/Migrations/20260711171406_InitialPersistence.cs',
  'src/Talabat/Talabat.Infrastructure/Persistence/Migrations/20260711171406_InitialPersistence.Designer.cs',
  'src/Talabat/Talabat.Infrastructure/Persistence/Migrations/20260715120523_AddIdentitySchema.cs',
  'src/Talabat/Talabat.Infrastructure/Persistence/Migrations/20260715120523_AddIdentitySchema.Designer.cs',
  'src/Talabat/Talabat.Infrastructure/Persistence/Migrations/20260716134242_AddCustomerIdentityUserId.cs',
  'src/Talabat/Talabat.Infrastructure/Persistence/Migrations/20260716134242_AddCustomerIdentityUserId.Designer.cs',
  'src/Talabat/Talabat.Infrastructure/Persistence/Migrations/InitialPersistence.idempotent.sql',
  'src/Talabat/Talabat.Infrastructure/Persistence/Migrations/TalabatDbContextModelSnapshot.cs'
)

$repoRoot = (Resolve-Path '.').Path
$migrationRoot = (Resolve-Path 'src/Talabat/Talabat.Infrastructure/Persistence/Migrations').Path
$resolvedTargets = $phase2MigrationFiles | ForEach-Object {
  $resolved = (Resolve-Path -LiteralPath $_).Path
  if (-not $resolved.StartsWith($migrationRoot + [IO.Path]::DirectorySeparatorChar,
      [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove migration outside $migrationRoot: $resolved"
  }
  $resolved
}

$resolvedTargets | ForEach-Object { Remove-Item -LiteralPath $_ -Force }

dotnet ef migrations add InitialUnifiedUser `
  --project src/Talabat/Talabat.Infrastructure `
  --startup-project src/Talabat/Talabat.API `
  --output-dir Persistence/Migrations
```

This step edits repository files only. It does not run `database drop` or `database update`.

Review generated source against
[contracts/persistence-schema.md](contracts/persistence-schema.md), then run:

```powershell
Get-ChildItem src/Talabat/Talabat.Infrastructure/Persistence/Migrations -File |
  Sort-Object Name |
  Select-Object -ExpandProperty Name

dotnet ef migrations list --no-connect `
  --project src/Talabat/Talabat.Infrastructure `
  --startup-project src/Talabat/Talabat.API

dotnet ef migrations has-pending-model-changes `
  --project src/Talabat/Talabat.Infrastructure `
  --startup-project src/Talabat/Talabat.API
```

Required:

- directory contains only one `InitialUnifiedUser` pair and the snapshot;
- migration list contains only `InitialUnifiedUser`; and
- pending model command reports no pending changes.

Now implement WP8B/WP9B in US1, US2, US3, and US4 order. These registration, sign-in, and
compatibility changes are schema-neutral; their disposable SQL tests use the reviewed migration.
Do not proceed to the pre-rebuild gate until every story's focused tests pass.

## 3. Full code/test acceptance before the rebuild

```powershell
dotnet build src/Talabat/Talabat.slnx
dotnet test src/Talabat/Talabat.slnx --no-build
dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive
```

All four suites must pass. SQL-dependent tests may report the fixture's existing explicit skip when
neither Docker/Testcontainers nor LocalDB is available; Phase 2 acceptance still requires those
tests to be run successfully in an environment with SQL Server before the development rebuild.

### Required behavior tests

Confirm the suite contains and passes:

- customer registration → one user, Customer flag and role;
- applicant registration → PendingApproval, no DeliveryAgent flag/role/status;
- approval → same user, flag and role, Offline status;
- duplicate email → validation and no persisted user/role/profile;
- missing-role/role failure → complete rollback;
- inactive login → 401;
- soft-deleted login → 401;
- User rowversion conflict → `ConcurrencyConflict`/409 mapping;
- exact raw ProfileNotCreated 404 and 409 JSON;
- malformed/non-positive subject → empty 401; and
- migrated existing customer/cart/order/checkout/delivery behavior.

### Removed-symbol sweeps

Run against production C# only and exclude generated migrations/obj:

```powershell
$removedPatterns = @(
  'ApplicationUser',
  'ICustomerRepository',
  'IDeliveryAgentRepository',
  'CustomerRepository',
  'DeliveryAgentRepository',
  'IdentityUserId',
  'IdentityDbContext<ApplicationUser',
  'class Customer\b',
  'class DeliveryAgent\b'
)

foreach ($pattern in $removedPatterns) {
  $hits = rg -n --glob '*.cs' --glob '!**/obj/**' --glob '!**/Persistence/Migrations/**' `
    $pattern src/Talabat
  if ($LASTEXITCODE -eq 0) {
    throw "Removed production symbol still exists: $pattern`n$hits"
  }
  if ($LASTEXITCODE -gt 1) {
    throw "rg failed for pattern: $pattern"
  }
}
```

Intentional retained symbols include `CustomerId`, `AssignedAgentId`, `CustomerProfile`,
`CustomerAddressDetails`, and `DeliveryAgentStatus`.

### Boundary sweeps

```powershell
rg -n "UserManager|RoleManager|SignInManager|ClaimsPrincipal|HttpContext|DbContext|EntityFrameworkCore" `
  src/Talabat/Talabat.Domain src/Talabat/Talabat.Application

rg -n "AddToRoleAsync|RemoveFromRoleAsync|UserType\s*[|&^]?=" src/Talabat `
  --glob '*.cs' --glob '!**/obj/**' --glob '!**/Persistence/Migrations/**'
```

Review every result. Allowed:

- Domain `User` internally mutating its own `UserType` in the approved methods;
- Application contract names/doc comments without framework types; and
- user-role membership calls only inside `UserCapabilityService`.

## 4. Checkpoint gate

Review the diff and migration. Then create the approved Phase 2 checkpoint using the repository's
normal commit workflow. After the commit:

```powershell
git status --short
```

Output must be empty. If a commit is not authorized, stop here. Do not rebuild the development DB.
Capture `git rev-parse HEAD` in the execution transcript. From this point through schema inspection
and the remaining read-only gates, do not update tracked task checkboxes or evidence; those updates
are committed together in the final evidence step.

## 5. Exact connection safety gate

Both development settings must resolve to the same explicitly authorized disposable database.

```powershell
Add-Type -AssemblyName System.Data

$phase2ConfigFiles = @(
  'src/Talabat/Talabat.API/appsettings.Development.json',
  'src/Talabat/Talabat.Identity/appsettings.Development.json'
)

$expectedServer = 'DESKTOP-5IHGJ9F\SQLEXPRESS'
$expectedDatabase = 'Talabat'

function Assert-AuthorizedTalabatConnection {
  param(
    [Parameter(Mandatory)] [string] $ConnectionString,
    [Parameter(Mandatory)] [string] $SourceName
  )

  $connection = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($ConnectionString)
  if (-not [string]::Equals($connection.DataSource, $expectedServer,
      [StringComparison]::OrdinalIgnoreCase) -or
      -not [string]::Equals($connection.InitialCatalog, $expectedDatabase,
      [StringComparison]::OrdinalIgnoreCase) -or
      -not $connection.IntegratedSecurity) {
    throw "STOP: unauthorized database target in $SourceName"
  }
}

$authorizedConnections = [ordered]@{}
foreach ($configFile in $phase2ConfigFiles) {
  $config = Get-Content -LiteralPath $configFile -Raw | ConvertFrom-Json
  $connectionString = [string]$config.ConnectionStrings.TalabatDb
  Assert-AuthorizedTalabatConnection -ConnectionString $connectionString -SourceName $configFile
  $authorizedConnections[$configFile] = $connectionString
}

# Six non-destructive negative cases: three mutations for each configuration source.
foreach ($entry in $authorizedConnections.GetEnumerator()) {
  foreach ($mutation in 'Server', 'Catalog', 'IntegratedSecurity') {
    $candidate = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($entry.Value)
    switch ($mutation) {
      'Server' { $candidate.DataSource = 'UNAUTHORIZED\SQLEXPRESS' }
      'Catalog' { $candidate.InitialCatalog = 'TalabatProd' }
      'IntegratedSecurity' { $candidate.IntegratedSecurity = $false }
    }

    $rejected = $false
    try {
      Assert-AuthorizedTalabatConnection `
        -ConnectionString $candidate.ConnectionString `
        -SourceName "$($entry.Key) [$mutation negative test]"
    }
    catch {
      $rejected = $true
    }

    if (-not $rejected) {
      throw "STOP: negative connection test unexpectedly passed: $($entry.Key) [$mutation]"
    }
  }
}
```

If `System.Data.SqlClient.SqlConnectionStringBuilder` is unavailable in the shell, stop before the
checkpoint and add/test an equivalent helper using `Microsoft.Data.SqlClient`, then repeat all
pre-rebuild gates and create a new checkpoint. Do not add a helper after the clean checkpoint and do
not fall back to substring matching.

Re-run immediately before destruction:

```powershell
git status --short
dotnet build src/Talabat/Talabat.slnx --no-restore
dotnet test src/Talabat/Talabat.slnx --no-build
```

Status must be empty and both commands green.

## 6. Authorized disposable development database rebuild

Only after sections 0–5 pass:

```powershell
dotnet ef database drop --force `
  --project src/Talabat/Talabat.Infrastructure `
  --startup-project src/Talabat/Talabat.API

dotnet ef database update `
  --project src/Talabat/Talabat.Infrastructure `
  --startup-project src/Talabat/Talabat.API
```

Start Identity to seed role definitions, wait for successful startup, then stop it cleanly:

```powershell
dotnet run --project src/Talabat/Talabat.Identity
```

No migration generation or source deletion occurs in this section; the already-tested migration is
applied.

## 7. Schema inspection

```powershell
sqlcmd -S "DESKTOP-5IHGJ9F\SQLEXPRESS" -d Talabat -E -Q @"
SELECT name FROM sys.tables ORDER BY name;

SELECT name, definition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('AspNetUsers')
ORDER BY name;

SELECT name, is_unique, filter_definition
FROM sys.indexes
WHERE object_id = OBJECT_ID('UserAddresses')
ORDER BY name;

SELECT
    OBJECT_NAME(fk.parent_object_id) AS DependentTable,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS DependentColumn,
    OBJECT_NAME(fk.referenced_object_id) AS PrincipalTable
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
WHERE OBJECT_NAME(fk.parent_object_id) IN ('Carts', 'Orders', 'Deliveries', 'UserAddresses')
ORDER BY DependentTable, DependentColumn;

SELECT Name FROM AspNetRoles ORDER BY Name;
SELECT COUNT(*) AS SeededUserCount FROM AspNetUsers;
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
"@
```

Verify against [contracts/persistence-schema.md](contracts/persistence-schema.md):

- no Customers, CustomerAddresses, or DeliveryAgents table;
- all eight `CK_Users_*` checks;
- exact unique filtered `UX_UserAddresses_UserId_Default`;
- Cart/Order/Delivery customer and assigned-agent FKs reference AspNetUsers;
- exactly Customer, DeliveryAgent, Admin, RestaurantOwner roles;
- startup seeding created no user (the count is zero immediately after a clean rebuild); and
- exactly one `InitialUnifiedUser` migration-history row.

Run the Identity host a second time and repeat the roles query; still exactly four role rows must
exist.

## 8. Final Phase 2 gate

```powershell
dotnet build src/Talabat/Talabat.slnx
dotnet test src/Talabat/Talabat.slnx --no-build
dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive
git status --short
```

If schema inspection produced no source change, status remains clean. After every remaining
read-only acceptance command passes, obtain authorization for the final evidence commit before
editing tracked files. If authorized, update the deferred checkboxes and record command summaries,
the pre-rebuild checkpoint commit, and inspection evidence in `tasks.md`; mark the final evidence
task complete before committing, and do not write that commit's own hash into a tracked file. Then
require `git status --short` to be empty. If authorization is unavailable, keep the evidence in the
execution transcript and leave the tree clean. Do not begin Phase 3.

## Failure and rollback rules

- **Before database drop**: stop, fix the owning work package, rerun gates. The development database
  is unchanged.
- **After drop but before successful update/seed**: the database is disposable and may be absent or
  incomplete. Fix source, rerun all code/test gates, make a new checkpoint, validate connections,
  then repeat drop/update. Do not attempt data recovery.
- **Code rollback**: revert the Phase 2 checkpoint through the repository's approved Git workflow,
  then rebuild the disposable database from the reverted code only if explicitly requested.
- Never delete or move a directory computed from an unchecked variable, and never target a
  production/shared database.
