## US-001 Dashboard Metrics

Status: Complete

### User Story
As a developer using dede,
I want a dashboard showing key counts,
so that I can quickly understand the solution's shape.

### Acceptance Criteria
- Given the UI loads successfully, when data is available, then the dashboard shows counts for endpoints, controllers, services, repositories, and EF Core entities.
- Given the user is viewing a filtered chain or alternate graph view, when dashboard metrics are displayed, then they still represent the entire solution/repo.
- Given one or more categories have zero entries, when counts are shown, then the category is displayed with value `0`.
- Given data cannot be loaded, when the dashboard area renders, then each unavailable metric shows `-` instead of a stale or misleading numeric value.

### Notes
- Metrics are read-only in this story.
- The React UI currently derives metrics from the full loaded graph document, so the design supports whole-solution counts independent of filtered views.
- The filtered/alternate graph-view nuance is not yet demonstrated end-to-end in the React UI because those views are not implemented there yet.
