import { Factory } from "fishery";
import type { GraphDocument } from "../../types/graph";

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
