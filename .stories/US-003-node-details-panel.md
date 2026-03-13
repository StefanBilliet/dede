## US-003 Node Details Panel

### User Story
As a developer using dede,
I want to click a node and inspect its details in a side panel,
so that I can understand the node without leaving the graph.

### Acceptance Criteria
- Given the graph is loaded, when the user selects a node, then the details panel opens on the right side.
- Given a node is selected, when the panel renders, then all fields available in that node's contract are displayed.
- Given a selected node has optional or missing fields, when details are shown, then the panel renders safely without crashing.
- Given another node is clicked, when selection changes, then the details panel updates to the new node.

### Notes
- Panel content is read-only in this story.
