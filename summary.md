# eShopOnWeb.vazor Migration Summary

## Migration Status: ✅ COMPLETE — Zero Compilation Errors

`dotnet build eShopOnWeb.vazor.sln` exits with code 0 and **0 errors**.

---

## Changes Made

### Project Files

**ApplicationCore.vb/ApplicationCore.vb.vbproj**
- Upgraded `<TargetFramework>` from `netstandard2.1` → `net10.0` (VB libraries stay single-TFM per KB rules)
- Upgraded `Ardalis.GuardClauses` 1.4.2 → 4.6.0

**Infrastructure.vb/Infrastructure.vb.vbproj**
- Upgraded `<TargetFramework>` from `netstandard2.1` → `net10.0`
- Removed duplicate/conflicting `Microsoft.AspNetCore.Identity 2.2.0` (already covered by Identity.EntityFrameworkCore)
- Upgraded `Ardalis.EFCore.Extensions` 1.1.0 → 6.0.0 (pinned to the actually-resolved version to clear NU1603)
- Upgraded `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 3.1.0 → 10.0.14
- Upgraded `Microsoft.EntityFrameworkCore.SqlServer` 3.1.0 → 10.0.14
- Upgraded `Microsoft.EntityFrameworkCore.Tools` 3.1.0 → 10.0.14

**Web.VB/Web.VB.vbproj**
- Removed `Microsoft.CodeAnalysis 3.4.0` — conflicted with `Microsoft.VisualStudio.Web.CodeGeneration.Design 10.0.2` (NU1107)
- Removed `MediatR.Extensions.Microsoft.DependencyInjection 7.0.0` — merged into MediatR 12+
- Upgraded `MediatR` 7.0.0 → 12.4.1
- Upgraded `Swashbuckle.AspNetCore` 5.0.0-rc2 → 7.3.1 (replaced rc2 and rc4 with stable)
- Removed separate `Swashbuckle.AspNetCore.SwaggerUI 5.0.0-rc4` (bundled in main package)
- Added explicit `Newtonsoft.Json 13.0.4` (was implicitly transitive; lost when Swashbuckle no longer depends on it)
- Removed `Ardalis.ListStartupServices 1.1.3` — dev-only diagnostic tool; its old package metadata pulled in `Microsoft.NETCore.App 1.1.2` (high-severity NU1903)
- Marked `Microsoft.VisualStudio.Web.CodeGeneration.Design` as `PrivateAssets=all` (scaffolding design-time tool, not a runtime dep)

### Code Changes

**Web.VB/Startup.vb**
- Fixed MediatR 12.x API: `services.AddMediatR(GetType(...).Assembly)` → `services.AddMediatR(Sub(cfg) cfg.RegisterServicesFromAssemblyContaining(Of ...)())`
- Replaced obsolete `app.UseDatabaseErrorPage()` → `app.UseMigrationsEndPoint()` (from `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore`)
- Fixed BC42105: changed `Function` lambdas passed to `Action(Of T)` APIs to `Sub` lambdas:
  - `services.Configure(Of ServiceConfig)(Function(config) ...)` → `Sub(config) ...`
  - `services.Configure(Of CookiePolicyOptions)(Function(options) ...)` → `Sub(options) ...`
  - `services.ConfigureApplicationCookie(Function(options) ...)` → `Sub(options) ...`
  - `app.UseSwaggerUI(Function(c) ...)` → `Sub(c) ...`
  - `app.UseEndpoints(Function(endpoints) ...)` → `Sub(endpoints) ...`
- Removed `Ardalis.ListStartupServices` imports and usage (`UseShowAllServicesMiddleware`, `ServiceConfig` registration)
- Removed unused `_services As IServiceCollection` field

**Web.VB/Controllers/AccountController.vb**
- Added `Shadows` keyword to `SignIn` (×2) and `SignOut` action methods (BC40005: methods shadow overridable `ControllerBase` helpers added in ASP.NET Core 6+)

**ApplicationCore.vb/Exceptions/BasketNotFoundException.vb**
- Removed obsolete `Protected Sub New(SerializationInfo, StreamingContext)` constructor (SYSLIB0051: BinaryFormatter serialization constructor removed in .NET 10)

**Infrastructure.vb/Data/CatalogContextSeed.vb**
- Restructured retry logic to avoid BC36943 (`Await` is not allowed inside `Catch` in VB.NET). The exception is now captured in a variable and the retry `Await` is called after the `Try/Catch` block.

**Infrastructure.vb/Identity/Migrations/AppIdentityDbContextModelSnapshot.vb**
**Infrastructure.vb/Identity/Migrations/20170822214310_InitialIdentityModel.Designer.vb**
- Replaced `HasName(...)` → `HasDatabaseName(...)` (BC40000: `HasName` obsolete in EF Core)

---

## Remaining Warnings (NuGet Audit — Cannot Fix Offline)

These 6 unique warnings (duplicated per-project-per-TFM in output = 12 total) are all NuGet vulnerability advisories for transitive packages. They **do not affect compilation or correctness** but should be addressed when the environment has network access.

| Package | Version | Severity | Advisory | Source |
|---------|---------|----------|----------|--------|
| `System.Security.Cryptography.Pkcs` | 9.0.11 | High + Moderate | GHSA-846f-f764-76mx / GHSA-2fmv-rv2c-59x7 | Transitive via `Microsoft.Data.SqlClient 6.1.7` ← EF Core SQL Server |
| `NuGet.Packaging` | 6.12.1 | Low | GHSA-g4vj-cjjj-v7hg | Transitive via `Microsoft.VisualStudio.Web.CodeGeneration.Design 10.0.2` (design-time only) |
| `NuGet.Protocol` | 6.12.1 | Low | GHSA-g4vj-cjjj-v7hg | Same as above |

---

## Next Steps

1. **System.Security.Cryptography.Pkcs**: When online, upgrade `Microsoft.Data.SqlClient` to a version that depends on a fixed `System.Security.Cryptography.Pkcs` (>= 10.0.x), or add a direct `<PackageReference Include="System.Security.Cryptography.Pkcs" Version="10.x.x" />` to pin to a safe version.

2. **NuGet.Packaging / NuGet.Protocol**: These come only from the design-time scaffolding tool (`Microsoft.VisualStudio.Web.CodeGeneration.Design`). Upgrading that package when a newer version is available offline will resolve these low-severity warnings. They have no runtime impact.

3. **CacheHelpers.vb**: `Extensions\CacheHelpers.vb` is currently excluded from compilation (`<None Include="Extensions\CacheHelpers.vb" />`). The file contains a `Module` with `Public Shared` members which is incorrect VB.NET syntax (`Shared` is implicit in a `Module`). To re-enable it: remove the `Shared` keyword from the members and move the file back to `<Compile>`.

4. **Option Strict On**: The library and web projects do not explicitly set `<OptionStrict>On</OptionStrict>`. Consider enabling it per-project once the migration is stable to surface any remaining latent type errors.

5. **Migration snapshots**: The Identity migration snapshot uses the old `Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityRole` string key. If the database is recreated, re-run `dotnet ef migrations add` to regenerate clean snapshots targeting EF Core 10.
