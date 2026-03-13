## US-005 Downstream Chain Focus

### User Story
As a developer using dede,
I want to view the full downstream chain of a selected node,
so that I can understand impact and dependent flow.

### Acceptance Criteria
- Given a node is selected, when the user chooses `Downstream`, then only nodes and edges in the full downstream chain are shown.
- Given the selected node has no downstream relations, when `Downstream` is chosen, then an explicit empty-state message is shown.
- Given downstream traversal contains cycles, when rendered, then the UI indicates a cycle without infinite expansion.
- Given downstream mode is active, when the user selects a different node and chooses `Downstream`, then the chain updates for that node.

### Notes
- Unrelated nodes are hidden, not dimmed.
