import {
  createDashboardMetrics,
  createUnavailableMetrics,
} from "./dashboardMetrics";
import type { GraphDocument } from "../types/graph";

function createGraphDocument(nodeTypes: string[]): GraphDocument {
  return {
    nodes: nodeTypes.map((type) => ({ type })),
  };
}

describe("dashboardMetrics", () => {
  test("WHEN graph contains dashboard node types THEN it counts each metric", () => {
    const document = createGraphDocument([
      "Endpoint",
      "Endpoint",
      "Controller",
      "Service",
      "Repository",
      "Entity",
      "Entity",
    ]);

    const metrics = createDashboardMetrics(document);

    expect(metrics).toEqual([
      { id: "endpoints", label: "Endpoints", value: 2 },
      { id: "controllers", label: "Controllers", value: 1 },
      { id: "services", label: "Services", value: 1 },
      { id: "repositories", label: "Repositories", value: 1 },
      { id: "entities", label: "EF Core entities", value: 2 },
    ]);
  });

  test("WHEN graph is missing dashboard node types THEN it returns 0 for those metrics", () => {
    const document = createGraphDocument([]);

    const metrics = createDashboardMetrics(document);

    expect(metrics).toEqual([
      { id: "endpoints", label: "Endpoints", value: 0 },
      { id: "controllers", label: "Controllers", value: 0 },
      { id: "services", label: "Services", value: 0 },
      { id: "repositories", label: "Repositories", value: 0 },
      { id: "entities", label: "EF Core entities", value: 0 },
    ]);
  });

  test("WHEN metrics are unavailable THEN it returns placeholder values", () => {
    expect(createUnavailableMetrics()).toEqual([
      { id: "endpoints", label: "Endpoints", value: "-" },
      { id: "controllers", label: "Controllers", value: "-" },
      { id: "services", label: "Services", value: "-" },
      { id: "repositories", label: "Repositories", value: "-" },
      { id: "entities", label: "EF Core entities", value: "-" },
    ]);
  });
});
