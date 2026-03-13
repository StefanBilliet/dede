## US-007 Cycle-Safe Visualization

### User Story
As a developer using dede,
I want cycle-aware graph rendering,
so that traversal views stay understandable and do not loop forever.

### Acceptance Criteria
- Given graph data contains one or more cycles, when the graph is rendered, then rendering completes and remains interactive.
- Given a cycle appears in upstream/downstream traversal results, when shown, then the UI indicates that a cycle exists.
- Given cycle indication is displayed, when the user inspects the graph, then the same logical node is not repeatedly expanded infinitely.
- Given no cycles exist, when rendering occurs, then no cycle indicators are shown.

### Notes
- Visual clarity is preferred over strict path duplication.
