# Contributing

One maintainer, one homelab. The conventions exist so that quality is checked
by something other than the author, not to add ceremony.

## The flow

1. **Issue first.** What is wrong or missing, why it matters, acceptance
   criteria. Typo fixes are exempt.
2. **Branch** `<type>/<short-description>`, lowercase: `feat/tv-input`,
   `fix/dns-stats`.
3. **Open a draft PR** as soon as there is something to push:
   `gh pr create --draft`, body `Closes #N`. CI runs on every push; the
   reviewer does not.
4. **Mark it ready when it is actually done:** `gh pr ready <n>`. This
   triggers the automated Claude review, an independent instance that did
   not write the code. A non-draft PR is reviewed on every push, so do not
   open one non-draft. Comment `@claude` on a PR to ask it a question.
5. **Answer every review comment and resolve the thread.** One of:
   - *Fixed*: say what changed and in which commit.
   - *Not fixing*: say why.
   - *Later*: open an issue and link it.
6. **Squash merge** once CI and `review` are green. The PR title becomes
   the commit subject on `main`.

PRs that change `.github/workflows/claude-review.yml` cannot be reviewed by
the action; see "Independent review" in `CLAUDE.md`.

## Commits and PR titles

[Conventional Commits](https://www.conventionalcommits.org/):
`<type>(<scope>): <subject>`, with the *why* in the body. Types: `feat`,
`fix`, `test`, `docs`, `ci`, `refactor`, `chore`.

No attribution trailers or "Generated with" footers, in branch commits too:
a squash merge copies branch-commit trailers into `main`.

## What CI enforces

- `dotnet format --verify-no-changes`
- `dotnet build` and `dotnet test` (Release)
- CodeQL
- Claude review ran (on non-draft PRs)

## Review secret

The review job authenticates with the repository secret
`CLAUDE_CODE_OAUTH_TOKEN`, minted with `claude setup-token` and set by the
owner:

```sh
claude setup-token
gh secret set CLAUDE_CODE_OAUTH_TOKEN --repo moudlajs/homelab
```
