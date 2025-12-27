# Homelab CLI - Development Rules

## Project
C# CLI for Mac Mini M4 homelab management using Spectre.Console

## Non-Negotiables
1. **Every feature = PR** (even solo)
2. **Tests before commit** (`dotnet test`)
3. **Use `/code-review`** before pushing
4. **Format code** (`dotnet format`)
5. **Conventional commits** (feat:, fix:, docs:)

## Workflow
```bash
git checkout -b feature/name
# ... make changes ...
dotnet test
/code-review
git commit -m "feat: description"
git push origin feature/name
```

## Architecture
- Commands/ = UI layer (Spectre.Console)
- Services/ = Business logic
- Models/ = Data structures
- Use interfaces for testability (IDockerService, etc.)

## Current Work
👉 **See `docs/CURRENT_PHASE.md` for active tasks**

## References
- Implementation guide: `docs/IMPLEMENTATION.md`
- Long-term roadmap: `docs/ROADMAP.md`