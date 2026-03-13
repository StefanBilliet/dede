export type GraphNode = {
  type: string;
};

export type GraphDocument = {
  nodes: GraphNode[];
};

export type DashboardMetric = {
  label: string;
  value: number | "-";
};

const metricDefinitions = [
  { label: "Endpoints", nodeType: "Endpoint" },
  { label: "Controllers", nodeType: "Controller" },
  { label: "Services", nodeType: "Service" },
  { label: "Repositories", nodeType: "Repository" },
  { label: "EF Core entities", nodeType: "Entity" },
] as const;

export function createUnavailableMetrics(): DashboardMetric[] {
  return metricDefinitions.map(({ label }) => ({ label, value: "-" }));
}

export function createDashboardMetrics(document: GraphDocument): DashboardMetric[] {
  return metricDefinitions.map(({ label, nodeType }) => ({
    label,
    value: document.nodes.filter((node) => node.type === nodeType).length,
  }));
}
