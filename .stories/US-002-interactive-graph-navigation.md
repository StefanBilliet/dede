## US-002 Interactive Graph Navigation

### User Story
As a developer using dede,
I want to pan and zoom around the graph,
so that I can inspect large structures without losing context.

### Acceptance Criteria
- Given the graph is visible, when the user drags the canvas, then the graph pans in the drag direction.
- Given the graph is visible, when the user zooms in or out, then the graph scale updates and remains legible.
- Given repeated pan and zoom actions, when navigation continues, then interactions stay responsive and do not freeze.
- Given a node is currently selected, when pan/zoom occurs, then selection remains active.

### Notes
- This story covers navigation behavior only, not selection/filter behavior.
