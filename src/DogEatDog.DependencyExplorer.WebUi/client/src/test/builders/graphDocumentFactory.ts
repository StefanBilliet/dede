import { Factory } from "fishery";

type SourceLocation = {
  filePath: string;
  line?: number;
  column?: number;
};

type ScanWarning = {
  code: string;
  message: string;
  path?: string;
  certainty: string;
  metadata?: Record<string, string | null>;
};

type StageTiming = {
  stage: string;
  elapsed: string;
};

type ScanMetadata = {
  rootPath: string;
  startedAtUtc: string;
  completedAtUtc: string;
  timings: StageTiming[];
  properties: Record<string, string>;
};

type ScanStatistics = {
  repositoryCount: number;
  solutionCount: number;
  projectCount: number;
  endpointCount: number;
  methodCount: number;
  httpEdgeCount: number;
  tableCount: number;
  crossRepoLinkCount: number;
  ambiguousEdgeCount: number;
};

type GraphNode = {
  id: string;
  type: string;
  displayName: string;
  sourceLocation?: SourceLocation;
  repositoryName?: string;
  projectName?: string;
  certainty: string;
  metadata: Record<string, string | null>;
};

type GraphEdge = {
  id: string;
  sourceId: string;
  targetId: string;
  type: string;
  displayName: string;
  sourceLocation?: SourceLocation;
  repositoryName?: string;
  projectName?: string;
  certainty: string;
  metadata: Record<string, string | null>;
};

export type GraphDocument = {
  nodes: GraphNode[];
  edges: GraphEdge[];
  scanMetadata: ScanMetadata;
  warnings: ScanWarning[];
  unresolved: ScanWarning[];
  statistics: ScanStatistics;
};

export const graphDocumentFactory = Factory.define<GraphDocument>(() => ({
  nodes: [],
  edges: [],
  scanMetadata: {
    rootPath: "/tmp/workspace",
    startedAtUtc: "2026-03-13T10:00:00Z",
    completedAtUtc: "2026-03-13T10:00:01Z",
    timings: [],
    properties: {},
  },
  warnings: [],
  unresolved: [],
  statistics: {
    repositoryCount: 0,
    solutionCount: 0,
    projectCount: 0,
    endpointCount: 0,
    methodCount: 0,
    httpEdgeCount: 0,
    tableCount: 0,
    crossRepoLinkCount: 0,
    ambiguousEdgeCount: 0,
  },
}));
