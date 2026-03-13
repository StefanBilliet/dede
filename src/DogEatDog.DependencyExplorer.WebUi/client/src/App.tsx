import { Badge, Group, Paper, Stack, Text, Title } from "@mantine/core";
import { useEffect, useState } from "react";
import { Background, Controls, MiniMap, ReactFlow } from "reactflow";
import {
  createDashboardMetrics,
  createUnavailableMetrics,
  type DashboardMetric,
  type GraphDocument,
} from "./metrics/dashboardMetrics";
import "reactflow/dist/style.css";
import {Metrics} from "./metrics/metrics.tsx";

const nodes = [
  {
    id: "endpoint",
    position: { x: 80, y: 160 },
    data: { label: "GET /api/orders/{id}" },
    style: { borderRadius: 12 },
  },
  {
    id: "service",
    position: { x: 380, y: 120 },
    data: { label: "OrderService.GetById" },
    style: { borderRadius: 12 },
  },
  {
    id: "table",
    position: { x: 700, y: 200 },
    data: { label: "orders table" },
    style: { borderRadius: 12 },
  },
];

const edges = [
  {
    id: "e-endpoint-service",
    source: "endpoint",
    target: "service",
    animated: true,
  },
  { id: "e-service-table", source: "service", target: "table" },
];

function App() {
  const [metrics, setMetrics] = useState<DashboardMetric[]>(() => createUnavailableMetrics());

  useEffect(() => {
    let cancelled = false;

    async function loadGraph() {
      try {
        const response = await fetch("/api/graph");
        if (!response.ok) {
          throw new Error("Graph load failed");
        }

        const document = (await response.json()) as GraphDocument;
        if (cancelled) {
          return;
        }

        setMetrics(createDashboardMetrics(document));
      } catch {
        if (!cancelled) {
          setMetrics(createUnavailableMetrics());
        }
      }
    }

    loadGraph();

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <main className="app-shell">
      <header className="hero">
        <Group justify="space-between" align="center">
          <Stack gap={4}>
            <Text size="xs" tt="uppercase" fw={700} c="dimmed">
              DogEatDog.DependencyExplorer
            </Text>
            <Title order={1}>dede React UI</Title>
            <Text c="dimmed">Vite 8 + Mantine + React Flow scaffold</Text>
          </Stack>
          <Badge color="teal" variant="light">
            Legacy UI remains at /
          </Badge>
        </Group>
      </header>

      <Metrics metrics={metrics} />

      <Paper shadow="sm" radius="lg" withBorder className="graph-panel">
        <ReactFlow nodes={nodes} edges={edges} fitView>
          <MiniMap />
          <Controls />
          <Background />
        </ReactFlow>
      </Paper>
    </main>
  );
}

export default App;
