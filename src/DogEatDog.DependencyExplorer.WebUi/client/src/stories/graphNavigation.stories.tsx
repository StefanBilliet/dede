import { Badge, Group, Paper, Stack, Text, Title } from "@mantine/core";
import type { Meta, StoryObj } from "@storybook/react";
import {
  DependencyGraph,
  type DependencyGraphEdgeStyle,
  type DependencyGraphInitialFraming,
} from "../graph/dependencyGraph";
import type { GraphDocument, GraphEdge, GraphNode } from "../types/graph";

type GraphScene = "ServicePath" | "DenseWorkspace";

type SceneDefinition = {
  label: string;
  description: string;
  document: GraphDocument;
};

type GraphNavigationStoryProps = {
  scene: GraphScene;
  initialFraming: DependencyGraphInitialFraming;
  edgeStyle: DependencyGraphEdgeStyle;
  selectedNodeId?: string;
  showMiniMap: boolean;
  showControls: boolean;
};

function createGraphNode(
  id: string,
  type: string,
  displayName: string,
  layout: { x: number; y: number },
): GraphNode {
  return {
    id,
    type,
    displayName,
    certainty: "Exact",
    metadata: {
      layoutX: String(layout.x),
      layoutY: String(layout.y),
    },
  };
}

function createGraphEdge(
  id: string,
  sourceId: string,
  targetId: string,
  animated = false,
): GraphEdge {
  return {
    id,
    sourceId,
    targetId,
    type: animated ? "AnimatedDependency" : "Dependency",
    displayName: `${sourceId} -> ${targetId}`,
    certainty: "Exact",
    metadata: {
      animated: animated ? "true" : "false",
    },
  };
}

const scenes: Record<GraphScene, SceneDefinition> = {
  ServicePath: {
    label: "API to data path",
    description:
      "A readable path for discussing how panning keeps nearby dependencies in view.",
    document: {
      nodes: [
        createGraphNode("endpoint-orders", "Endpoint", "GET /api/orders/{id}", {
          x: 80,
          y: 150,
        }),
        createGraphNode("orders-controller", "Controller", "OrdersController", {
          x: 330,
          y: 80,
        }),
        createGraphNode("order-service", "Service", "OrderService.GetById", {
          x: 340,
          y: 230,
        }),
        createGraphNode("pricing-client", "Client", "PricingClient", {
          x: 620,
          y: 60,
        }),
        createGraphNode("order-repository", "Repository", "OrderRepository", {
          x: 630,
          y: 220,
        }),
        createGraphNode("orders-table", "Table", "orders table", {
          x: 910,
          y: 220,
        }),
        createGraphNode("audit-log", "Table", "audit log", { x: 910, y: 360 }),
      ],
      edges: [
        createGraphEdge("e1", "endpoint-orders", "orders-controller", true),
        createGraphEdge("e2", "orders-controller", "order-service"),
        createGraphEdge("e3", "order-service", "pricing-client"),
        createGraphEdge("e4", "order-service", "order-repository", true),
        createGraphEdge("e5", "order-repository", "orders-table"),
        createGraphEdge("e6", "order-service", "audit-log"),
      ],
    },
  },
  DenseWorkspace: {
    label: "Cross-project workspace",
    description:
      "A denser graph for reviewing legibility when navigation moves between clusters.",
    document: {
      nodes: [
        createGraphNode("api-gateway", "Gateway", "ApiGateway", {
          x: 80,
          y: 120,
        }),
        createGraphNode("orders-api", "Api", "Orders API", { x: 260, y: 40 }),
        createGraphNode("billing-api", "Api", "Billing API", {
          x: 260,
          y: 220,
        }),
        createGraphNode("orders-core", "Project", "Orders.Core", {
          x: 520,
          y: 20,
        }),
        createGraphNode("orders-data", "Project", "Orders.Data", {
          x: 520,
          y: 170,
        }),
        createGraphNode("billing-core", "Project", "Billing.Core", {
          x: 520,
          y: 330,
        }),
        createGraphNode("identity-sdk", "Library", "Identity SDK", {
          x: 800,
          y: 10,
        }),
        createGraphNode("sql-cluster", "Database", "SQL Cluster", {
          x: 820,
          y: 180,
        }),
        createGraphNode("event-bus", "Queue", "Event Bus", { x: 800, y: 340 }),
        createGraphNode("warehouse-job", "Worker", "Warehouse Sync", {
          x: 1080,
          y: 120,
        }),
        createGraphNode("reporting-ui", "Ui", "Reporting UI", {
          x: 1080,
          y: 290,
        }),
        createGraphNode("alerts-worker", "Worker", "Alerts Worker", {
          x: 1320,
          y: 220,
        }),
      ],
      edges: [
        createGraphEdge("d1", "api-gateway", "orders-api", true),
        createGraphEdge("d2", "api-gateway", "billing-api"),
        createGraphEdge("d3", "orders-api", "orders-core"),
        createGraphEdge("d4", "orders-api", "orders-data"),
        createGraphEdge("d5", "billing-api", "billing-core"),
        createGraphEdge("d6", "orders-core", "identity-sdk"),
        createGraphEdge("d7", "orders-data", "sql-cluster", true),
        createGraphEdge("d8", "billing-core", "event-bus"),
        createGraphEdge("d9", "event-bus", "warehouse-job"),
        createGraphEdge("d10", "sql-cluster", "warehouse-job"),
        createGraphEdge("d11", "warehouse-job", "reporting-ui"),
        createGraphEdge("d12", "event-bus", "alerts-worker", true),
        createGraphEdge("d13", "reporting-ui", "alerts-worker"),
      ],
    },
  },
};

const framingLabels: Record<DependencyGraphInitialFraming, string> = {
  fitView: "Fit view overview",
  wide: "Wide canvas framing",
  zoomedIn: "Zoomed detail framing",
};

const edgeStyleLabels: Record<DependencyGraphEdgeStyle, string> = {
  default: "Bezier flow",
  straight: "Straight lines",
  step: "Step lines",
  smoothstep: "Smooth step lines",
};

function GraphNavigationStory({
  scene,
  initialFraming,
  edgeStyle,
  selectedNodeId,
  showMiniMap,
  showControls,
}: GraphNavigationStoryProps) {
  const sceneDefinition = scenes[scene];

  return (
    <Paper
      radius="xl"
      p="lg"
      withBorder
      style={{
        maxWidth: 1400,
        margin: "0 auto",
        background: "rgba(255, 255, 255, 0.76)",
        backdropFilter: "blur(10px)",
      }}
    >
      <Stack gap="md">
        <Group justify="space-between" align="flex-start">
          <Stack gap={4}>
            <Text size="xs" tt="uppercase" fw={700} c="dimmed">
              US-002 Interactive Graph Navigation
            </Text>
            <Title order={2}>Navigation framing study</Title>
            <Text c="dimmed" maw={720}>
              {sceneDefinition.description}
            </Text>
          </Stack>
          <Group gap="xs">
            <Badge color="teal" variant="light">
              {sceneDefinition.label}
            </Badge>
            <Badge color="blue" variant="light">
              {framingLabels[initialFraming]}
            </Badge>
            <Badge color="cyan" variant="light">
              {edgeStyleLabels[edgeStyle]}
            </Badge>
            {selectedNodeId ? (
              <Badge color="grape" variant="light">
                Selection retained
              </Badge>
            ) : null}
          </Group>
        </Group>

        <Paper
          radius="lg"
          withBorder
          style={{
            height: 720,
            overflow: "hidden",
            backgroundImage:
              "linear-gradient(to right, rgba(10, 30, 40, 0.06) 1px, transparent 1px), linear-gradient(to bottom, rgba(10, 30, 40, 0.06) 1px, transparent 1px)",
            backgroundSize: "24px 24px",
          }}
        >
          <DependencyGraph
            document={sceneDefinition.document}
            selectedNodeId={selectedNodeId}
            initialFraming={initialFraming}
            edgeStyle={edgeStyle}
            showMiniMap={showMiniMap}
            showControls={showControls}
          />
        </Paper>
      </Stack>
    </Paper>
  );
}

const meta = {
  title: "Graph/Navigation",
  component: GraphNavigationStory,
  args: {
    scene: "ServicePath",
    initialFraming: "fitView",
    edgeStyle: "default",
    selectedNodeId: undefined,
    showMiniMap: true,
    showControls: true,
  },
  argTypes: {
    scene: {
      control: { type: "radio" },
      options: Object.keys(scenes),
    },
    initialFraming: {
      control: { type: "radio" },
      options: Object.keys(framingLabels),
    },
    edgeStyle: {
      control: { type: "radio" },
      options: Object.keys(edgeStyleLabels),
    },
    selectedNodeId: {
      control: { type: "text" },
    },
  },
} satisfies Meta<typeof GraphNavigationStory>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Overview: Story = {};

export const ZoomedInSelectionRetained: Story = {
  args: {
    initialFraming: "zoomedIn",
    edgeStyle: "straight",
    selectedNodeId: "order-repository",
  },
};

export const DenseWorkspace: Story = {
  args: {
    scene: "DenseWorkspace",
    initialFraming: "wide",
    edgeStyle: "step",
    selectedNodeId: "warehouse-job",
  },
};
