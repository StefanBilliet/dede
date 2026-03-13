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
import type { GraphDocument } from "../types/graph";

export type DependencyGraphInitialFraming = "fitView" | "wide" | "zoomedIn";
export type DependencyGraphEdgeStyle = keyof Pick<
  EdgeTypes,
  "default" | "straight" | "step" | "smoothstep"
>;

export type DependencyGraphProps = {
  document: GraphDocument;
  selectedNodeId?: string;
  initialFraming?: DependencyGraphInitialFraming;
  edgeStyle?: DependencyGraphEdgeStyle;
  showMiniMap?: boolean;
  showControls?: boolean;
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

const defaultViewports: Record<
  Exclude<DependencyGraphInitialFraming, "fitView">,
  Viewport
> = {
  wide: { x: -120, y: -10, zoom: 0.72 },
  zoomedIn: { x: -360, y: -100, zoom: 1.18 },
};

function createFallbackPosition(index: number) {
  const column = index % 4;
  const row = Math.floor(index / 4);

  return {
    x: 120 + column * 260,
    y: 100 + row * 170,
  };
}

function createNodes(document: GraphDocument, selectedNodeId?: string): Node[] {
  return document.nodes
    .map((node, index) => ({
      id: node.id ?? node.displayName ?? `${node.type}-${index}`,
      position: {
        x: Number(node.metadata?.layoutX ?? Number.NaN),
        y: Number(node.metadata?.layoutY ?? Number.NaN),
      },
      data: { label: node.displayName ?? node.id ?? node.type },
      selected: node.id === selectedNodeId,
      style:
        node.id === selectedNodeId
          ? { ...defaultNodeStyle, ...selectedNodeStyle }
          : defaultNodeStyle,
    }))
    .map((node, index) => ({
      ...node,
      position:
        Number.isFinite(node.position.x) && Number.isFinite(node.position.y)
          ? node.position
          : createFallbackPosition(index),
    }));
}

function createEdges(
  document: GraphDocument,
  edgeStyle: DependencyGraphEdgeStyle,
): Edge[] {
  return (document.edges ?? []).map((edge) => ({
    id: edge.id ?? `${edge.sourceId}-${edge.targetId}`,
    source: edge.sourceId,
    target: edge.targetId,
    type: edgeStyle,
    animated: edge.metadata?.animated === "true",
    style: { stroke: "#6f7f86", strokeWidth: 1.5 },
  }));
}

export function DependencyGraph({
  document,
  selectedNodeId,
  initialFraming = "fitView",
  edgeStyle = "default",
  showMiniMap = true,
  showControls = true,
}: DependencyGraphProps) {
  const fitView = initialFraming === "fitView";

  return (
    <ReactFlow
      nodes={createNodes(document, selectedNodeId)}
      edges={createEdges(document, edgeStyle)}
      fitView={fitView}
      fitViewOptions={{ padding: 0.18 }}
      defaultViewport={fitView ? undefined : defaultViewports[initialFraming]}
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
  );
}
