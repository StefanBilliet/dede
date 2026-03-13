import { Badge, Group, Paper, Stack, Text, Title } from "@mantine/core";
import { Background, Controls, MiniMap, ReactFlow } from "reactflow";
import "reactflow/dist/style.css";

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
