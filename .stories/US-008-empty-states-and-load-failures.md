## US-008 Empty States and Load Failures

### User Story
As a developer using dede,
I want clear empty and failure states,
so that I always understand whether there is no data or an error occurred.

### Acceptance Criteria
- Given graph data is empty, when the graph area renders, then a clear empty-state message is shown.
- Given graph data fails to load, when the graph area renders, then a visible error state is shown.
- Given dashboard data fails to load, when the dashboard area renders, then unavailable metrics show `-` instead of numeric values.
- Given the user requests upstream/downstream on a node with no chain, when results are computed, then a specific no-results message is shown.
- Given data later becomes available after an error or empty state, when the UI refreshes, then graph and metrics render normally.

### Notes
- Messages should distinguish "no data" from "failed to load".
