# Domain Layer Quickstart

This guide outlines how to build and work with the Talabat.Domain layer, based on Phase 2 of the implementation roadmap.

## Rules of Engagement
1. **Zero Dependencies:** The `Talabat.Domain` project must compile without any external NuGet packages.
2. **Rich Models:** Never use `public set` for entity properties.
3. **Aggregate Enforcement:** All cross-entity rules must be evaluated inside methods on the Aggregate Root.

## Next Steps for Developers
1. Run `dotnet new classlib -n Talabat.Domain -o src/Talabat.Domain`.
2. Delete `Class1.cs`.
3. Create the directories: `/ValueObjects`, `/Exceptions`, `/Entities`, `/Interfaces`.
4. Begin implementing `Money` and `TimeRange` value objects.
5. Implement the entities defined in `data-model.md`.
