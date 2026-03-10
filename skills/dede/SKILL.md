---
name: dede
description: Run DogEatDog.DependencyExplorer against a local .NET workspace to scan multiple repositories, export a dependency graph, serve the local explorer UI, and answer blast-radius queries. Use when the user wants static dependency analysis, cross-repo dependency mapping, endpoint-to-database tracing, or impact analysis across one or many .NET codebases.
---

# dede

Use this skill to run DogEatDog.DependencyExplorer from an agent. The skill is portable across Codex, Claude Code, and OpenCode because it shells into the repository's CLI instead of assuming a globally installed binary.

## Quick Start

1. Identify the dede repository root.
   Use `--repo-root <path>` when invoking the wrapper if the current directory is not inside the repository.
2. Use `scripts/run-dede.sh` for all commands.
   The wrapper resolves `dotnet`, finds the repository root, and runs the CLI project directly.
3. Prefer these commands:
   - Scan a workspace: `scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer scan /path/to/workspace -o /path/to/graph.json`
   - Keep a graph up to date: `scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer watch /path/to/workspace -o /path/to/graph.json`
   - Serve the explorer: `scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer serve /path/to/graph.json --url http://127.0.0.1:5057`
   - Export Neo4j CSV: `scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer export neo4j /path/to/workspace -o /path/to/neo4j`
   - Run impact analysis: `scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer query impact --graph /path/to/graph.json --node "<id or name>"`
   - Find paths: `scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer query paths --graph /path/to/graph.json --from "<node>" --to "<node>"`

See `references/commands.md` for more examples.

## Workflow

### 1. Establish context

- Confirm the workspace root the user wants analyzed.
- Confirm or infer the dede repository root.
- If the repository root is not the current working directory, pass `--repo-root`.

### 2. Produce or load a graph

- If the user wants fresh analysis, run `scan` or `export json`.
- Prefer `watch` instead of repeated manual `scan` calls when the user is actively editing a workspace.
- If the user already has a graph artifact, use it for `serve` or `query`.
- Default output path should be explicit, for example `./artifacts/graph.json`, rather than relying on implicit files.
- Prefer a root-level `.dedeignore` file over very long `--exclude` lists. Use `--exclude-file` only when the ignore file lives elsewhere.

### 3. Answer the actual architecture question

- For downstream impact from an endpoint, controller, service, or method, use `query impact`.
- For an exact connection between two nodes, use `query paths`.
- For demo mode, run `serve` and point the user at the local URL.
- Summarize the important counts and findings instead of pasting raw JSON or long command output.

### 4. Preserve uncertainty

- Do not invent links the scanner did not resolve.
- If the graph contains ambiguous or unresolved edges, say so explicitly.
- When a lookup fails because the graph is missing or stale, regenerate it instead of guessing.

## Operational Notes

- The wrapper honors `DEDE_REPO_ROOT` if set.
- The wrapper also searches upward from the current directory for `DogEatDog.DependencyExplorer.sln`.
- The skill assumes the repository contains `src/DogEatDog.DependencyExplorer.Cli`.
- `dede` automatically loads `.dedeignore` from the scan root. Agents should rely on that first.
- If `dotnet` cannot be found, stop and report that the .NET SDK is missing.

## Cross-Agent Install Shape

This skill folder is self-contained and can be copied or symlinked into each agent's skill directory.

- Codex: `~/.codex/skills/dede`
- Claude Code: `~/.claude/skills/dede`
- GitHub Copilot: `~/.copilot/skills/dede` or `.github/skills/dede`
- OpenCode: `~/.config/opencode/skills/dede`, `.opencode/skills/dede`, `.claude/skills/dede`, or `.agents/skills/dede`

The `agents/openai.yaml` file provides OpenAI/Codex interface metadata. The standard `SKILL.md` folder layout is what Claude Code, OpenCode, and GitHub Copilot consume.

Run `scripts/install-all-agents.sh /path/to/DogEatDog.DependencyExplorer` to create both repo-local and home-directory links for the major agents.
