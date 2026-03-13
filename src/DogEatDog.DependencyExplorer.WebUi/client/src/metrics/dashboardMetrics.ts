import type { GraphDocument } from "../types/graph";

export type DashboardMetric = {
  id: string;
  label: string;
  value: number | "-";
};

const metricDefinitions = [
  { id: "endpoints", label: "Endpoints", nodeType: "Endpoint" },
  { id: "controllers", label: "Controllers", nodeType: "Controller" },
  { id: "services", label: "Services", nodeType: "Service" },
  { id: "repositories", label: "Repositories", nodeType: "Repository" },
  { id: "entities", label: "EF Core entities", nodeType: "Entity" },
] as const;

export function createUnavailableMetrics(): DashboardMetric[] {
  return metricDefinitions.map(({ id, label }) => ({ id, label, value: "-" }));
}

export function createDashboardMetrics(document: GraphDocument): DashboardMetric[] {
  return metricDefinitions.map(({ id, label, nodeType }) => ({
    id,
    label,
    value: document.nodes.filter((node) => node.type === nodeType).length,
  }));
}
