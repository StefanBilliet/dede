const state = {
  graph: null,
  presets: [],
  selectedNodeId: null,
  currentView: "workspace-topology",
  currentSubgraph: null,
  pathResult: null,
};

const views = [
  { id: "workspace-topology", title: "Workspace topology" },
  { id: "service-dependencies", title: "Service/API dependencies" },
  { id: "mediatr-dispatch", title: "MediatR dispatch flow" },
  { id: "data-impact", title: "Data/table impact" },
  { id: "cross-repo", title: "Cross-repo map" },
  { id: "ambiguity-audit", title: "Unresolved audit" },
];

const colors = {
  Workspace: "#ff7a18",
  Repository: "#ffd166",
  Solution: "#4ecdc4",
  Project: "#6fa8dc",
  Controller: "#45b7d1",
  Endpoint: "#ff9f43",
  Service: "#06d6a0",
  Method: "#a0c4ff",
  Interface: "#cdb4db",
  Implementation: "#84dcc6",
  HttpClient: "#ff5d5d",
  ExternalService: "#f28482",
  ExternalEndpoint: "#ffa69e",
  DbContext: "#6c91bf",
  Entity: "#9d4edd",
  Table: "#f6bd60",
  ConfigurationKey: "#8aa2b7",
};

const edgeColors = {
  DISPATCHES: "#f9c74f",
  HANDLED_BY: "#06d6a0",
  CROSSES_REPO_BOUNDARY: "#ff5d5d",
};

const mediatrEdgeTypes = new Set(["DISPATCHES", "HANDLED_BY"]);

boot();

async function boot() {
  bindEvents();
  renderViewButtons();
  await loadPresets();
  await loadGraph();
}

function bindEvents() {
  document.getElementById("scanButton").addEventListener("click", scanWorkspace);
  document.getElementById("downstreamButton").addEventListener("click", () => queryImpact("downstream"));
  document.getElementById("upstreamButton").addEventListener("click", () => queryImpact("upstream"));
  document.getElementById("pathButton").addEventListener("click", findPaths);

  [
    "repoFilter",
    "projectFilter",
    "nodeTypeFilter",
    "edgeTypeFilter",
    "searchInput",
    "exactOnlyToggle",
    "ambiguousToggle",
  ].forEach((id) => document.getElementById(id).addEventListener("input", render));
}

async function loadGraph() {
  const response = await fetch("/api/graph");
  if (!response.ok) {
    renderStatus("No graph loaded. Enter a workspace root and scan.");
    render();
    return;
  }

  state.graph = await response.json();
  state.currentSubgraph = null;
  state.pathResult = null;
  renderStatus(`Loaded graph for ${state.graph.ScanMetadata.RootPath}`);
  populateFilters();
  renderSummary();
  render();
}

async function loadPresets() {
  const response = await fetch("/api/presets");
  state.presets = response.ok ? await response.json() : [];
  const host = document.getElementById("presetList");
  host.innerHTML = "";
  state.presets.forEach((preset) => {
    const wrapper = document.createElement("div");
    wrapper.className = "preset";
    wrapper.innerHTML = `<strong>${preset.title}</strong><p>${preset.description}</p>`;
    const button = document.createElement("button");
    button.textContent = "Apply";
    button.className = "secondary";
    button.addEventListener("click", () => applyPreset(preset.id, preset.direction));
    wrapper.appendChild(button);
    host.appendChild(wrapper);
  });
}

async function scanWorkspace() {
  const rootPath = document.getElementById("rootPath").value.trim();
  if (!rootPath) {
    renderStatus("A workspace root is required.");
    return;
  }

  renderStatus(`Scanning ${rootPath} ...`);
  const response = await fetch("/api/scan", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ rootPath }),
  });

  if (!response.ok) {
    renderStatus("Scan failed.");
    return;
  }

  state.graph = await response.json();
  state.currentSubgraph = null;
  state.pathResult = null;
  populateFilters();
  renderSummary();
  renderStatus(`Scanned ${state.graph.ScanMetadata.RootPath}`);
  render();
}

async function queryImpact(direction) {
  const node = document.getElementById("searchInput").value.trim() || state.selectedNodeId;
  if (!node) {
    renderStatus("Select or search for a node first.");
    return;
  }

  const params = new URLSearchParams({
    node,
    direction,
    depth: document.getElementById("depthInput").value,
    exactOnly: document.getElementById("exactOnlyToggle").checked,
    includeAmbiguous: document.getElementById("ambiguousToggle").checked,
  });

  const response = await fetch(`/api/query/impact?${params}`);
  if (!response.ok) {
    renderStatus("Impact query failed.");
    return;
  }

  state.currentSubgraph = await response.json();
  state.selectedNodeId = state.currentSubgraph.FocusNode.Id;
  render();
}

async function findPaths() {
  const from = document.getElementById("pathFrom").value.trim();
  const to = document.getElementById("pathTo").value.trim();
  if (!from || !to) {
    return;
  }

  const params = new URLSearchParams({
    from,
    to,
    depth: document.getElementById("depthInput").value,
    exactOnly: document.getElementById("exactOnlyToggle").checked,
    includeAmbiguous: document.getElementById("ambiguousToggle").checked,
  });

  const response = await fetch(`/api/query/paths?${params}`);
  state.pathResult = response.ok ? await response.json() : null;
  renderPaths();
}

function applyPreset(id, direction) {
  const nodeTypeFilter = document.getElementById("nodeTypeFilter");
  const edgeTypeFilter = document.getElementById("edgeTypeFilter");

  if (id === "table-impact") {
    nodeTypeFilter.value = "Table";
    state.currentView = "data-impact";
  } else if (id === "cross-repo") {
    edgeTypeFilter.value = "CROSSES_REPO_BOUNDARY";
    state.currentView = "cross-repo";
  } else if (id === "endpoint-downstream") {
    nodeTypeFilter.value = "Endpoint";
    state.currentView = "service-dependencies";
  } else if (id === "service-callers") {
    nodeTypeFilter.value = "Service";
    state.currentView = "service-dependencies";
  } else if (id === "mediatr-dispatch") {
    edgeTypeFilter.value = "All";
    state.currentView = "mediatr-dispatch";
  } else if (id === "ambiguity-audit") {
    state.currentView = "ambiguity-audit";
    document.getElementById("ambiguousToggle").checked = true;
  }

  renderViewButtons();
  render();
  if (direction) {
    queryImpact(direction);
  }
}

function populateFilters() {
  if (!state.graph) {
    return;
  }

  populateSelect("repoFilter", ["All", ...unique(state.graph.Nodes.map((node) => node.RepositoryName).filter(Boolean))]);
  populateSelect("projectFilter", ["All", ...unique(state.graph.Nodes.map((node) => node.ProjectName).filter(Boolean))]);
  populateSelect("nodeTypeFilter", ["All", ...unique(state.graph.Nodes.map((node) => node.Type))]);
  populateSelect("edgeTypeFilter", ["All", ...unique(state.graph.Edges.map((edge) => edge.Type))]);
}

function populateSelect(id, values) {
  const select = document.getElementById(id);
  const previousValue = select.value;
  select.innerHTML = values.map((value) => `<option value="${value}">${value}</option>`).join("");
  if (values.includes(previousValue)) {
    select.value = previousValue;
  } else {
    select.value = "All";
  }
}

function renderViewButtons() {
  const host = document.getElementById("viewButtons");
  host.innerHTML = "";
  views.forEach((view) => {
    const button = document.createElement("button");
    button.textContent = view.title;
    button.className = state.currentView === view.id ? "" : "secondary";
    button.addEventListener("click", () => {
      state.currentView = view.id;
      renderViewButtons();
      render();
    });
    host.appendChild(button);
  });
}

function renderSummary() {
  const host = document.getElementById("summaryTiles");
  if (!state.graph) {
    host.innerHTML = "";
    return;
  }

  const stats = state.graph.Statistics;
  const mediatrEdgeCount = state.graph.Edges.filter((edge) => mediatrEdgeTypes.has(edge.Type)).length;
  host.innerHTML = [
    tile("Repos scanned", stats.RepositoryCount),
    tile("Projects", stats.ProjectCount),
    tile("Endpoints", stats.EndpointCount),
    tile("Methods", stats.MethodCount),
    tile("MediatR edges", mediatrEdgeCount),
    tile("HTTP edges", stats.HttpEdgeCount),
    tile("DB tables", stats.TableCount),
    tile("Cross-repo links", stats.CrossRepoLinkCount),
    tile("Ambiguous edges", stats.AmbiguousEdgeCount),
  ].join("");
}

function tile(label, value) {
  return `<div class="tile"><span>${label}</span><strong>${value}</strong></div>`;
}

function render() {
  renderSummary();
  renderGraph();
  renderAudit();
  renderNodeDetails();
  renderPaths();
}

function renderGraph() {
  const svg = document.getElementById("graphCanvas");
  svg.innerHTML = "";

  const graph = getWorkingGraph();
  if (!graph.nodes.length) {
    svg.innerHTML = `<text x="40" y="60" fill="#edf5fb">No nodes match the current filters. Reset filters to All or clear the search box.</text>`;
    return;
  }

  const layout = createLayout(graph);

  graph.edges.forEach((edge) => {
    const source = layout.positions[edge.SourceId];
    const target = layout.positions[edge.TargetId];
    if (!source || !target) {
      return;
    }

    const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
    const midX = (source.x + target.x) / 2;
    path.setAttribute("d", `M ${source.x} ${source.y} C ${midX} ${source.y}, ${midX} ${target.y}, ${target.x} ${target.y}`);
    path.setAttribute("fill", "none");
    path.setAttribute("stroke", resolveEdgeStroke(edge));
    path.setAttribute("stroke-width", resolveEdgeWidth(edge));
    path.setAttribute("stroke-dasharray", resolveEdgeDash(edge));
    path.setAttribute("stroke-opacity", "0.7");
    svg.appendChild(path);
  });

  graph.nodes.forEach((node) => {
    const position = layout.positions[node.Id];
    const group = document.createElementNS("http://www.w3.org/2000/svg", "g");
    group.style.cursor = "pointer";
    group.addEventListener("click", () => {
      state.selectedNodeId = node.Id;
      document.getElementById("searchInput").value = node.DisplayName;
      renderNodeDetails();
    });

    const rect = document.createElementNS("http://www.w3.org/2000/svg", "rect");
    rect.setAttribute("x", position.x - 88);
    rect.setAttribute("y", position.y - 24);
    rect.setAttribute("width", 176);
    rect.setAttribute("height", 48);
    rect.setAttribute("rx", 14);
    rect.setAttribute("fill", colors[node.Type] || "#6fa8dc");
    rect.setAttribute("fill-opacity", state.selectedNodeId === node.Id ? "0.95" : "0.75");
    rect.setAttribute("stroke", state.selectedNodeId === node.Id ? "#ffffff" : "#0b1720");
    rect.setAttribute("stroke-width", state.selectedNodeId === node.Id ? "2.5" : "1");

    const label = document.createElementNS("http://www.w3.org/2000/svg", "text");
    label.setAttribute("x", position.x);
    label.setAttribute("y", position.y - 2);
    label.setAttribute("text-anchor", "middle");
    label.setAttribute("font-size", "12");
    label.setAttribute("font-family", "IBM Plex Sans, sans-serif");
    label.setAttribute("fill", "#08141f");
    label.textContent = node.DisplayName.slice(0, 24);

    const sub = document.createElementNS("http://www.w3.org/2000/svg", "text");
    sub.setAttribute("x", position.x);
    sub.setAttribute("y", position.y + 13);
    sub.setAttribute("text-anchor", "middle");
    sub.setAttribute("font-size", "10");
    sub.setAttribute("font-family", "IBM Plex Mono, monospace");
    sub.setAttribute("fill", "#08141f");
    sub.textContent = node.Type;

    group.append(rect, label, sub);
    svg.appendChild(group);
  });
}

function renderNodeDetails() {
  const host = document.getElementById("nodeDetails");
  const nodes = state.currentSubgraph?.Nodes || state.graph?.Nodes || [];
  const node = nodes.find((item) => item.Id === state.selectedNodeId) || state.graph?.Nodes.find((item) => item.Id === state.selectedNodeId);

  if (!node) {
    host.textContent = "Select a node to inspect metadata and blast radius context.";
    return;
  }

  const metadata = Object.entries(node.Metadata || {})
    .map(([key, value]) => `<div><strong>${key}</strong><br>${value ?? ""}</div>`)
    .join("");

  host.innerHTML = `
    <div class="metadata">
      <div><strong>${node.DisplayName}</strong><br>${node.Type} · ${node.Certainty}</div>
      <div><strong>Repository</strong><br>${node.RepositoryName || "n/a"}</div>
      <div><strong>Project</strong><br>${node.ProjectName || "n/a"}</div>
      <div><strong>Source</strong><br>${formatLocation(node.SourceLocation)}</div>
      ${metadata}
    </div>
    <div class="legend">
      ${Object.entries(colors).slice(0, 8).map(([name, color]) => `<span><i style="background:${color}"></i>${name}</span>`).join("")}
      ${Object.entries(edgeColors).filter(([name]) => mediatrEdgeTypes.has(name)).map(([name, color]) => `<span><i style="background:${color}"></i>${name}</span>`).join("")}
    </div>
  `;
}

function renderAudit() {
  const host = document.getElementById("auditPanel");
  if (!state.graph) {
    host.innerHTML = "";
    return;
  }

  const auditEdges = state.graph.Edges.filter((edge) => edge.Certainty !== "Exact").slice(0, 40);
  host.innerHTML = `<h2>Ambiguous / Unresolved Audit</h2>${auditEdges.map((edge) => `
    <div class="audit-item">
      <strong>${edge.Type}</strong>
      <div>${edge.DisplayName}</div>
      <small>${edge.Certainty} · ${edge.RepositoryName || "n/a"} / ${edge.ProjectName || "n/a"}</small>
    </div>
  `).join("")}`;
}

function renderPaths() {
  const host = document.getElementById("pathResults");
  if (!state.pathResult) {
    host.innerHTML = "";
    return;
  }

  host.innerHTML = state.pathResult.Paths.slice(0, 8).map((path, index) => `
    <div class="path-item">
      <strong>Path ${index + 1}</strong>
      <div>${path.Nodes.map((node) => node.DisplayName).join(" → ")}</div>
    </div>
  `).join("");
}

function getWorkingGraph() {
  if (!state.graph) {
    return { nodes: [], edges: [] };
  }

  const source = state.currentSubgraph
    ? { nodes: state.currentSubgraph.Nodes, edges: state.currentSubgraph.Edges }
    : { nodes: state.graph.Nodes, edges: state.graph.Edges };

  const repoFilter = document.getElementById("repoFilter").value;
  const projectFilter = document.getElementById("projectFilter").value;
  const nodeTypeFilter = document.getElementById("nodeTypeFilter").value;
  const edgeTypeFilter = document.getElementById("edgeTypeFilter").value;
  const search = document.getElementById("searchInput").value.trim().toLowerCase();
  const exactOnly = document.getElementById("exactOnlyToggle").checked;
  const includeAmbiguous = document.getElementById("ambiguousToggle").checked;

  let nodes = source.nodes.filter((node) =>
    (repoFilter === "All" || node.RepositoryName === repoFilter) &&
    (projectFilter === "All" || node.ProjectName === projectFilter) &&
    (nodeTypeFilter === "All" || node.Type === nodeTypeFilter) &&
    (!search || node.DisplayName.toLowerCase().includes(search) || node.Id.toLowerCase().includes(search)));

  let nodeSet = new Set(nodes.map((node) => node.Id));
  let edges = source.edges.filter((edge) =>
    nodeSet.has(edge.SourceId) &&
    nodeSet.has(edge.TargetId) &&
    (edgeTypeFilter === "All" || edge.Type === edgeTypeFilter) &&
    (!exactOnly || edge.Certainty === "Exact") &&
    (includeAmbiguous || edge.Certainty === "Exact" || edge.Certainty === "Inferred"));

  ({ nodes, edges } = applyViewFilter(nodes, edges));
  const related = new Set(edges.flatMap((edge) => [edge.SourceId, edge.TargetId]));
  nodes = nodes.filter((node) => related.has(node.Id) || node.Id === state.selectedNodeId).slice(0, 160);
  nodeSet = new Set(nodes.map((node) => node.Id));
  edges = edges.filter((edge) => nodeSet.has(edge.SourceId) && nodeSet.has(edge.TargetId)).slice(0, 260);

  return { nodes, edges };
}

function applyViewFilter(nodes, edges) {
  const nodeIds = new Set(nodes.map((node) => node.Id));

  if (state.currentView === "workspace-topology") {
    const allowed = new Set(["Workspace", "Repository", "Solution", "Project"]);
    return filterByTypes(nodes, edges, allowed, null);
  }

  if (state.currentView === "service-dependencies") {
    const allowed = new Set(["Controller", "Endpoint", "Service", "Implementation", "Interface", "Method", "HttpClient", "ExternalService", "ExternalEndpoint", "Project", "Repository"]);
    return filterByTypes(nodes, edges, allowed, null);
  }

  if (state.currentView === "mediatr-dispatch") {
    const filteredEdges = edges.filter((edge) => mediatrEdgeTypes.has(edge.Type));
    const related = new Set(filteredEdges.flatMap((edge) => [edge.SourceId, edge.TargetId]));
    return { nodes: nodes.filter((node) => related.has(node.Id) && nodeIds.has(node.Id)), edges: filteredEdges };
  }

  if (state.currentView === "data-impact") {
    const allowed = new Set(["Endpoint", "Controller", "Service", "Implementation", "Method", "DbContext", "Entity", "Table", "Project"]);
    return filterByTypes(nodes, edges, allowed, null);
  }

  if (state.currentView === "cross-repo") {
    const filteredEdges = edges.filter((edge) => edge.Type === "CROSSES_REPO_BOUNDARY");
    const related = new Set(filteredEdges.flatMap((edge) => [edge.SourceId, edge.TargetId]));
    return { nodes: nodes.filter((node) => related.has(node.Id) && nodeIds.has(node.Id)), edges: filteredEdges };
  }

  if (state.currentView === "ambiguity-audit") {
    const filteredEdges = edges.filter((edge) => edge.Certainty !== "Exact");
    const related = new Set(filteredEdges.flatMap((edge) => [edge.SourceId, edge.TargetId]));
    return { nodes: nodes.filter((node) => related.has(node.Id)), edges: filteredEdges };
  }

  return { nodes, edges };
}

function filterByTypes(nodes, edges, allowedNodeTypes) {
  const filteredNodes = nodes.filter((node) => allowedNodeTypes.has(node.Type));
  const ids = new Set(filteredNodes.map((node) => node.Id));
  return { nodes: filteredNodes, edges: edges.filter((edge) => ids.has(edge.SourceId) && ids.has(edge.TargetId)) };
}

function createLayout(graph) {
  const positions = {};
  const groups = groupByDepth(graph);
  const columnWidth = 250;
  const rowHeight = 82;

  Object.entries(groups).forEach(([depth, nodes]) => {
    nodes.forEach((node, index) => {
      positions[node.Id] = {
        x: 140 + Number(depth) * columnWidth,
        y: 80 + index * rowHeight,
      };
    });
  });

  return { positions };
}

function groupByDepth(graph) {
  if (state.currentSubgraph?.FocusNode) {
    const start = state.currentSubgraph.FocusNode.Id;
    const queue = [[start, 0]];
    const depthMap = new Map([[start, 0]]);
    const outgoing = new Map();
    graph.edges.forEach((edge) => {
      if (!outgoing.has(edge.SourceId)) {
        outgoing.set(edge.SourceId, []);
      }
      outgoing.get(edge.SourceId).push(edge.TargetId);
    });

    while (queue.length) {
      const [nodeId, depth] = queue.shift();
      (outgoing.get(nodeId) || []).forEach((targetId) => {
        if (!depthMap.has(targetId)) {
          depthMap.set(targetId, depth + 1);
          queue.push([targetId, depth + 1]);
        }
      });
    }

    return graph.nodes.reduce((acc, node) => {
      const depth = depthMap.get(node.Id) ?? 0;
      if (!acc[depth]) {
        acc[depth] = [];
      }
      acc[depth].push(node);
      return acc;
    }, {});
  }

  const order = ["Workspace", "Repository", "Solution", "Project", "Controller", "Endpoint", "Service", "Implementation", "Interface", "Method", "HttpClient", "ExternalService", "ExternalEndpoint", "DbContext", "Entity", "Table", "ConfigurationKey"];
  return graph.nodes.reduce((acc, node) => {
    const depth = Math.max(order.indexOf(node.Type), 0);
    if (!acc[depth]) {
      acc[depth] = [];
    }
    acc[depth].push(node);
    return acc;
  }, {});
}

function renderStatus(message) {
  document.getElementById("scanStatus").textContent = message;
}

function formatLocation(location) {
  if (!location) {
    return "n/a";
  }

  return `${location.FilePath}${location.Line ? `:${location.Line}` : ""}`;
}

function unique(values) {
  return [...new Set(values)].sort((a, b) => a.localeCompare(b));
}

function resolveEdgeStroke(edge) {
  if (edgeColors[edge.Type]) {
    return edgeColors[edge.Type];
  }

  return edge.Certainty === "Exact" ? "#4ecdc4" : edge.Certainty === "Inferred" ? "#ffd166" : "#ff5d5d";
}

function resolveEdgeWidth(edge) {
  if (edge.Type === "CROSSES_REPO_BOUNDARY") {
    return "3";
  }

  if (mediatrEdgeTypes.has(edge.Type)) {
    return "2.4";
  }

  return "1.4";
}

function resolveEdgeDash(edge) {
  if (edge.Type === "DISPATCHES") {
    return "5 3";
  }

  return "";
}
