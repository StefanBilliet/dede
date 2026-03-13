import { Badge, Group, Paper, Stack, Text, Title } from "@mantine/core";
import { useQuery } from "@tanstack/react-query";
import { DependencyGraph } from "./graph/dependencyGraph";
import {
  createDashboardMetrics,
  createUnavailableMetrics,
} from "./metrics/dashboardMetrics";
import { Metrics } from "./metrics/metrics.tsx";
import type { GraphDocument } from "./types/graph";

async function fetchGraph(): Promise<GraphDocument> {
  const response = await fetch("/api/graph");
  if (!response.ok) {
    throw new Error("Graph load failed");
  }

  return await response.json();
}

function App() {
  const { data } = useQuery({
    queryKey: ["graph"],
    queryFn: fetchGraph,
    retry: false,
  });

  const metrics = data
    ? createDashboardMetrics(data)
    : createUnavailableMetrics();
  const graphDocument = data ?? { nodes: [], edges: [] };

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
        <DependencyGraph document={graphDocument} />
      </Paper>
    </main>
  );
}

export default App;
