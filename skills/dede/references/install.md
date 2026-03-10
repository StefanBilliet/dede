# install dede skill locally

This skill is designed to be shared across multiple agents from the same source folder.

## One-shot install

From the repository root:

```bash
./skills/dede/scripts/install-all-agents.sh "$(pwd)"
```

That creates repo-local links for `.github/skills`, `.claude/skills`, `.opencode/skills`, and `.agents/skills`, plus global links for `~/.codex/skills`, `~/.claude/skills`, `~/.copilot/skills`, and `~/.config/opencode/skills`.

## Codex

Create a symlink in `~/.codex/skills`:

```bash
ln -s /path/to/DogEatDog.DependencyExplorer/skills/dede ~/.codex/skills/dede
```

## Claude Code

Create a symlink in `~/.claude/skills`:

```bash
ln -s /path/to/DogEatDog.DependencyExplorer/skills/dede ~/.claude/skills/dede
```

## OpenCode

Preferred locations:

- project-local: `.opencode/skills/dede`
- global: `~/.config/opencode/skills/dede`
- compatible fallback: `.claude/skills/dede` or `.agents/skills/dede`

Optional alternative: add the parent skill folder to `~/.opencode/opencode.json.local`:

```json
{
  "skills": {
    "paths": [
      "/path/to/DogEatDog.DependencyExplorer/skills"
    ]
  }
}
```

OpenCode reads skill folders from the configured `skills.paths` entries.

## GitHub Copilot

Preferred locations:

- project-local: `.github/skills/dede`
- global: `~/.copilot/skills/dede`
