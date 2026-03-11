# Invariant 4: No External Dependencies in Core

## Rule
`Gluey.Contract` has zero NuGet package dependencies. Only .NET base framework types are used.

## Rationale
The core package must be conflict-free, lightweight, and usable in any .NET project without version conflicts.

## Verification
- `Gluey.Contract.csproj` has no `<PackageReference>` elements.
- CI check: fail build if a NuGet dependency is added to core.
