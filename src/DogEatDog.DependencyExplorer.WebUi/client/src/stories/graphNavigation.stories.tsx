import { Badge, Group, Paper, Stack, Text, Title } from "@mantine/core";
import type { Meta, StoryObj } from "@storybook/react";
import type { CSSProperties } from "react";
import {
  Background,
  BackgroundVariant,
  Controls,
  type Edge,
  type EdgeTypes,
  MiniMap,
  type Node,
  ReactFlow,
  type Viewport,
} from "reactflow";
import "reactflow/dist/style.css";
import type { GraphDocument, GraphEdge, GraphNode } from "../types/graph";

type GraphScene = "ServicePath" | "DenseWorkspace";
type GraphFraming = "FitView" | "Wide" | "ZoomedIn";
type GraphEdgeStyle = keyof Pick<
  EdgeTypes,
  "default" | "straight" | "step" | "smoothstep"
>;

type SceneDefinition = {
  label: string;
  description: string;
  document: GraphDocument;
};

type GraphNavigationStoryProps = {
  scene: GraphScene;
  framing: GraphFraming;
  edgeStyle: GraphEdgeStyle;
  selectedNodeId?: string;
  showMiniMap: boolean;
  showControls: boolean;
};

const selectedNodeStyle: CSSProperties = {
  border: "1px solid rgba(20, 78, 74, 0.55)",
  background: "linear-gradient(180deg, #f2fbfa 0%, #dff4ee 100%)",
  boxShadow: "0 0 0 3px rgba(46, 143, 127, 0.14)",
};

const defaultNodeStyle: CSSProperties = {
  borderRadius: 14,
  border: "1px solid rgba(20, 35, 45, 0.14)",
  background: "rgba(248, 251, 252, 0.96)",
  color: "#12313a",
  fontWeight: 600,
  padding: "10px 14px",
};

const scenes: Record<GraphScene, SceneDefinition> = {
  ServicePath: {
    label: "API to data path",
    description:
      "A readable path for discussing how panning keeps nearby dependencies in view.",
    document: {
      nodes: [
        createGraphNode("endpoint-orders", "Endpoint", "GET /api/orders/{id}"),
        createGraphNode("orders-controller", "Controller", "OrdersController"),
        createGraphNode("order-service", "Service", "OrderService.GetById"),
        createGraphNode("pricing-client", "Client", "PricingClient"),
        createGraphNode("order-repository", "Repository", "OrderRepository"),
        createGraphNode("orders-table", "Table", "orders table"),
        createGraphNode("audit-log", "Table", "audit log"),
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
        createGraphNode("api-gateway", "Gateway", "ApiGateway"),
        createGraphNode("orders-api", "Api", "Orders API"),
        createGraphNode("billing-api", "Api", "Billing API"),
        createGraphNode("orders-core", "Project", "Orders.Core"),
        createGraphNode("orders-data", "Project", "Orders.Data"),
        createGraphNode("billing-core", "Project", "Billing.Core"),
        createGraphNode("identity-sdk", "Library", "Identity SDK"),
        createGraphNode("sql-cluster", "Database", "SQL Cluster"),
        createGraphNode("event-bus", "Queue", "Event Bus"),
        createGraphNode("warehouse-job", "Worker", "Warehouse Sync"),
        createGraphNode("reporting-ui", "Ui", "Reporting UI"),
        createGraphNode("alerts-worker", "Worker", "Alerts Worker"),
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

const framingLabels: Record<GraphFraming, string> = {
  FitView: "Fit view overview",
  Wide: "Wide canvas framing",
  ZoomedIn: "Zoomed detail framing",
};

const edgeStyleLabels: Record<GraphEdgeStyle, string> = {
  default: "Bezier flow",
  straight: "Straight lines",
  step: "Step lines",
  smoothstep: "Smooth step lines",
};

const defaultViewports: Record<GraphFraming, Viewport> = {
  FitView: { x: 0, y: 0, zoom: 1 },
  Wide: { x: -120, y: -10, zoom: 0.72 },
  ZoomedIn: { x: -360, y: -100, zoom: 1.18 },
};

const sceneNodePositions: Record<
  GraphScene,
  Record<string, { x: number; y: number }>
> = {
  ServicePath: {
    "endpoint-orders": { x: 80, y: 150 },
    "orders-controller": { x: 330, y: 80 },
    "order-service": { x: 340, y: 230 },
    "pricing-client": { x: 620, y: 60 },
    "order-repository": { x: 630, y: 220 },
    "orders-table": { x: 910, y: 220 },
    "audit-log": { x: 910, y: 360 },
  },
  DenseWorkspace: {
    "api-gateway": { x: 80, y: 120 },
    "orders-api": { x: 260, y: 40 },
    "billing-api": { x: 260, y: 220 },
    "orders-core": { x: 520, y: 20 },
    "orders-data": { x: 520, y: 170 },
    "billing-core": { x: 520, y: 330 },
    "identity-sdk": { x: 800, y: 10 },
    "sql-cluster": { x: 820, y: 180 },
    "event-bus": { x: 800, y: 340 },
    "warehouse-job": { x: 1080, y: 120 },
    "reporting-ui": { x: 1080, y: 290 },
    "alerts-worker": { x: 1320, y: 220 },
  },
};

function createGraphNode(
  id: string,
  type: string,
  displayName: string,
): GraphNode {
  return {
    id,
    type,
    displayName,
    certainty: "Exact",
    metadata: {},
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

function createNodes(scene: GraphScene, selectedNodeId?: string): Node[] {
  return scenes[scene].document.nodes.map((node) => ({
    id: node.id ?? node.displayName ?? node.type,
    position: sceneNodePositions[scene][node.id ?? ""] ?? {
      x: 0,
      y: 0,
    },
    data: { label: node.displayName ?? node.id ?? node.type },
    selected: node.id === selectedNodeId,
    style:
      node.id === selectedNodeId
        ? { ...defaultNodeStyle, ...selectedNodeStyle }
        : defaultNodeStyle,
  }));
}

function createEdges(scene: GraphScene, edgeStyle: GraphEdgeStyle): Edge[] {
  return (scenes[scene].document.edges ?? []).map((edge) => ({
    id: edge.id ?? `${edge.sourceId}-${edge.targetId}`,
    source: edge.sourceId,
    target: edge.targetId,
    type: edgeStyle,
    animated: edge.metadata?.animated === "true",
    style: { stroke: "#6f7f86", strokeWidth: 1.5 },
  }));
}

function GraphNavigationStory({
  scene,
  framing,
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
              {framingLabels[framing]}
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
          <ReactFlow
            nodes={createNodes(scene, selectedNodeId)}
            edges={createEdges(scene, edgeStyle)}
            fitView={framing === "FitView"}
            fitViewOptions={{ padding: 0.18 }}
            defaultViewport={defaultViewports[framing]}
            nodesDraggable={false}
            nodesConnectable={false}
            elementsSelectable={false}
            panOnDrag={false}
            zoomOnDoubleClick={false}
            zoomOnPinch={false}
            zoomOnScroll={false}
            preventScrolling={false}
            minZoom={0.45}
            maxZoom={1.5}
            proOptions={{ hideAttribution: true }}
          >
            <Background variant={BackgroundVariant.Dots} gap={20} size={1} />
            {showMiniMap ? <MiniMap pannable zoomable /> : null}
            {showControls ? <Controls showInteractive={false} /> : null}
          </ReactFlow>
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
    framing: "FitView",
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
    framing: {
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
    framing: "ZoomedIn",
    edgeStyle: "straight",
    selectedNodeId: "order-repository",
  },
};

export const DenseWorkspace: Story = {
  args: {
    scene: "DenseWorkspace",
    framing: "Wide",
    edgeStyle: "step",
    selectedNodeId: "warehouse-job",
  },
};
