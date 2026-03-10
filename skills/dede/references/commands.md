# dede command reference

Use the wrapper script so the skill works even when `dede` is not installed as a global command.

For GitHub Copilot, the skill is easiest to trigger from `.github/skills/dede` or `~/.copilot/skills/dede`.
For OpenCode, use `.opencode/skills/dede`, `.claude/skills/dede`, `.agents/skills/dede`, or `~/.config/opencode/skills/dede`.

## Scan a workspace

```bash
scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer scan /path/to/workspace -o /path/to/graph.json
```

If the workspace has a `.dedeignore`, it is loaded automatically.

## Watch and refresh a graph continuously

```bash
scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer watch /path/to/workspace -o /path/to/graph.json
```

Use `--debounce-ms 2000` if the repo has noisy generators or large save bursts.

## Serve the local explorer

```bash
scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer serve /path/to/graph.json --url http://127.0.0.1:5057
```

## Export JSON

```bash
scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer export json /path/to/workspace -o /path/to/graph.json
```

## Export Neo4j CSV

```bash
scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer export neo4j /path/to/workspace -o /path/to/neo4j
```

## Downstream or upstream impact

```bash
scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer query impact --graph /path/to/graph.json --node "OrdersController.Get"
scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer query impact --graph /path/to/graph.json --node "Orders" --direction upstream
```

## Path lookup between two nodes

```bash
scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer query paths --graph /path/to/graph.json --from "OrdersController.Get" --to "sales.orders"
```

## Human-review report: ambiguous and unresolved edges

Prints a summary of all edges that require human review, grouped by certainty level:

- **Unresolved (certainty=0)**: edges the scanner could not resolve at all (e.g. unknown HTTP targets)
- **Ambiguous (certainty=1)**: edges that resolved to multiple candidates (e.g. interface calls with more than one implementation)
- **Unresolved symbols**: document-level warnings from the scan (e.g. MSBuild failures, unparseable config files)

```bash
scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer query ambiguous --graph /path/to/graph.json
# show more rows
scripts/run-dede.sh --repo-root /path/to/DogEatDog.DependencyExplorer query ambiguous --graph /path/to/graph.json --limit 200
```

## Repository root resolution

The wrapper finds the repository root in this order:

1. `--repo-root <path>`
2. `DEDE_REPO_ROOT`
3. Walk upward from the current working directory until `DogEatDog.DependencyExplorer.sln` is found
