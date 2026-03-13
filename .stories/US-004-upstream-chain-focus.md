## US-004 Upstream Chain Focus

### User Story
As a developer using dede,
I want to view the full upstream chain of a selected node,
so that I can understand what leads into it.

### Acceptance Criteria
- Given a node is selected, when the user chooses `Upstream`, then only nodes and edges in the full upstream chain are shown.
- Given the selected node has no upstream relations, when `Upstream` is chosen, then an explicit empty-state message is shown.
- Given upstream traversal contains cycles, when rendered, then the UI indicates a cycle without infinite expansion.
- Given upstream mode is active, when the user selects a different node and chooses `Upstream`, then the chain updates for that node.

### Notes
- Unrelated nodes are hidden, not dimmed.
