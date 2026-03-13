export type SourceLocation = {
  filePath: string;
  line?: number;
  column?: number;
};

export type ScanWarning = {
  code: string;
  message: string;
  path?: string;
  certainty: string;
  metadata?: Record<string, string | null>;
};

export type StageTiming = {
  stage: string;
  elapsed: string;
};

export type ScanMetadata = {
  rootPath: string;
  startedAtUtc: string;
  completedAtUtc: string;
  timings: StageTiming[];
  properties: Record<string, string>;
};

export type ScanStatistics = {
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

export type GraphNode = {
  id?: string;
  type: string;
  displayName?: string;
  sourceLocation?: SourceLocation;
  repositoryName?: string;
  projectName?: string;
  certainty?: string;
  metadata?: Record<string, string | null>;
};

export type GraphEdge = {
  id?: string;
  sourceId: string;
  targetId: string;
  type?: string;
  displayName?: string;
  sourceLocation?: SourceLocation;
  repositoryName?: string;
  projectName?: string;
  certainty?: string;
  metadata?: Record<string, string | null>;
};

export type GraphDocument = {
  nodes: GraphNode[];
  edges?: GraphEdge[];
  scanMetadata?: ScanMetadata;
  warnings?: ScanWarning[];
  unresolved?: ScanWarning[];
  statistics?: ScanStatistics;
};
