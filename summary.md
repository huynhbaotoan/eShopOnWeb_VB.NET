# Migration Summary – eShopOnWeb.vazor.sln (.NET Framework → .NET 10)

## Status
**BUILD SUCCEEDED — 0 compilation errors, 0 compiler warnings**  
`dotnet build eShopOnWeb.vazor.sln` exits with code 0.  
Remaining 20 warnings are NuGet package vulnerability advisories (NU190x) — all transitive, none actionable without newer package versions (see "Next steps").

---

## Changes Made

### 1. ApplicationCore.vb/ApplicationCore.vb.vbproj
- Changed `<TargetFramework>` from `netstandard2.1` → `net10.0`  
  (VB library projects stay single-TFM per KB 01 / 29 — no `netstandard2.0` multi-targeting for `.vbproj`)
- Added `<LangVersion>latest</LangVersion>`
- Kept `Ardalis.GuardClauses 1.4.2` (targets `netstandard2.0`, works on `net10.0` via BCL compatibility)

### 2. Infrastructure.vb/Infrastructure.vb.vbproj
- Changed `<TargetFramework>` from `netstandard2.1` → `net10.0`
- Added `<LangVersion>latest</LangVersion>`
- Removed `Microsoft.AspNetCore.Identity 2.2.0` — superseded by `Identity.EntityFrameworkCore`
- Updated `Microsoft.AspNetCore.Identity.EntityFrameworkCore` `3.1.0` → `10.0.14`
- Updated `Microsoft.EntityFrameworkCore.SqlServer` `3.1.0` → `10.0.14`
- Updated `Microsoft.EntityFrameworkCore.Tools` `3.1.0` → `10.0.14`
- Removed `Ardalis.EFCore.Extensions 1.1.0` — its `ApplyConfigurationsFromAssembly` extension was incorporated into EF Core itself since v3.0; the codebase already calls the native EF Core method

### 3. Web.VB/Web.VB.vbproj
- Removed `Microsoft.CodeAnalysis 3.4.0`: caused NU1107 version conflict with `Microsoft.VisualStudio.Web.CodeGeneration.Design 10.0.2` which requires `Microsoft.CodeAnalysis.CSharp.Workspaces >= 5.0.0`, while the pinned 3.4.0 locked it to exactly 3.4.0
- Marked `Microsoft.VisualStudio.Web.CodeGeneration.Design 10.0.2` with `<PrivateAssets>all</PrivateAssets>` — it is a scaffolding tool used only at design time, not at runtime; this prevents its transitive dependencies from polluting the published/consumable dependency graph

### 4. Web.VB/Startup.vb
- Replaced `app.UseDatabaseErrorPage()` with `app.UseMigrationsEndPoint()` — `UseDatabaseErrorPage` was removed in .NET 8+; `UseMigrationsEndPoint` (from `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore`, already referenced) is the modern equivalent

---

## Remaining Warnings (Next Steps)

All 20 remaining warnings are `NU190x` NuGet package vulnerability advisories for **transitive** packages. None can be resolved in an offline environment because the fixed versions are not present in the local NuGet cache. They require internet access / package-feed update to resolve.

| Warning | Package | Fixed Version | Root Cause | Notes |
|---------|---------|---------------|-----------|-------|
| NU1903 (high) | `Microsoft.NETCore.App 1.1.2` | N/A — phantom ref | `ZML.1.0 1.1.2` → `NETStandard.Library 2.0.3` includes framework-compat metadata packages from .NET Core 1.1. These are **not loaded at runtime** on .NET 10; they are NuGet-graph-only references from the `netstandard2.0` compatibility shim. No real CVE exposure at runtime. | Resolve by upgrading `ZML.1.0` to a newer version if one is published. |
| NU1903 (high) | `Microsoft.NETCore.Jit 1.1.2` | N/A — phantom ref | Same root as above | Same fix |
| NU1902/1903 | `System.Security.Cryptography.Pkcs 9.0.11` | `>= 9.0.14` | Transitive from `Microsoft.EntityFrameworkCore.Tools 10.0.14` and `Microsoft.VisualStudio.Web.CodeGeneration.Design 10.0.2` | Pin `System.Security.Cryptography.Pkcs` to `>= 9.0.14` once available in feed |
| NU1902 | `Swashbuckle.AspNetCore.SwaggerUI 5.0.0-rc4` | `>= 6.x stable` | Directly referenced in `Web.VB.vbproj` (project was originally built against this pre-release) | Upgrade `Swashbuckle.AspNetCore` to `6.9.0` or later when available; also update Swagger registration in Startup.vb per KB 23 |
| NU1901 | `NuGet.Packaging 6.12.1`, `NuGet.Protocol 6.12.1` | `>= 6.13.x` | Transitive from `Microsoft.VisualStudio.Web.CodeGeneration.Design 10.0.2` | Low-severity (affects build tool only, not runtime). Marked `PrivateAssets=all` on that package to document intent. Resolve by upgrading CodeGeneration.Design if a 10.0.x patch is released. |

---

## Architecture Notes

- All three projects now target `net10.0`
- VB.NET library projects (`.vbproj`) are kept as single-TFM per KB rules — `netstandard2.0` multi-targeting is C#-only
- EF Core configuration (`IEntityTypeConfiguration<T>`, `ApplyConfigurationsFromAssembly`) is all native EF Core 10 — no third-party EFCore extension packages needed
- Identity uses `Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.14` throughout
- Vazor/ZML view framework packages are retained as-is — they are the core view mechanism for this VB.NET + ASP.NET Core project
