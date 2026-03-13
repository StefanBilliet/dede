## Epic: New React UI for dede

### Problem
The existing UI can be improved for faster dependency comprehension and clearer day-to-day exploration workflows.

### Goal
Provide a graph-first React UI that improves comprehension of system structure and call/dependency chains.

### Scope
- Dashboard counts for key architectural elements.
- Interactive graph with pan/zoom.
- Node selection with details panel.
- Full-chain upstream/downstream exploration.
- Switch between solution layout and endpoints-focused views.

### Out of Scope
- Editing node data.
- New graph extraction/back-end contract changes.
- Team/role customizations.

### Success Indicators
- Users can quickly inspect a node and understand where requests/flows come from and go to.
- Users can switch graph contexts without losing orientation.
- UI remains readable when graph includes cycles and sparse chains.
