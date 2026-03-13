## US-006 Switch Graph Views

### User Story
As a developer using dede,
I want to switch between solution layout and endpoints-focused graph views,
so that I can analyze structure from different perspectives.

### Acceptance Criteria
- Given the UI opens, when no manual switch has been made, then the default graph view is solution layout.
- Given the user selects the endpoints view, when the view changes, then the graph updates to the endpoints-focused scope.
- Given the user switches back to solution layout, when the view changes, then the solution layout graph is restored.
- Given a selected node exists in both views, when switching views, then selection is preserved.
- Given a selected node does not exist in the new view, when switching views, then selection is cleared and the details panel shows no active selection.

### Decision
- Endpoints-focused scope includes endpoint nodes and their connected context/dependency nodes.
