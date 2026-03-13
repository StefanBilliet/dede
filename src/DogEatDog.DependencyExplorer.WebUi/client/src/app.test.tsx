import { MantineProvider } from "@mantine/core";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import type { ReactNode } from "react";
import App from "./app";
import { graphDocumentFactory } from "./test/builders/graphDocumentFactory";

const server = setupServer();

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

async function expectMetricValue(label: string, value: string) {
  const metric = await screen.findByRole("group", { name: label });
  expect(await within(metric).findByText(value)).toBeInTheDocument();
}

function createGraphNode(id: string, type: string) {
  return {
    id,
    type,
    displayName: `${type} ${id}`,
    certainty: "Exact",
    metadata: {},
  };
}

function renderWithProviders(ui: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  render(
    <QueryClientProvider client={queryClient}>
      <MantineProvider>{ui}</MantineProvider>
    </QueryClientProvider>,
  );
}

describe("App", () => {
  test("WHEN graph loads with dashboard node types THEN app shows their counts", async () => {
    server.use(
      http.get("/api/graph", () =>
        HttpResponse.json(
          graphDocumentFactory.build({
            nodes: [
              createGraphNode("endpoint-1", "Endpoint"),
              createGraphNode("controller-1", "Controller"),
              createGraphNode("service-1", "Service"),
              createGraphNode("repository-1", "Repository"),
              createGraphNode("entity-1", "Entity"),
            ],
          }),
        ),
      ),
    );

    renderWithProviders(<App />);

    await expectMetricValue("Endpoints", "1");
    await expectMetricValue("Controllers", "1");
    await expectMetricValue("Services", "1");
    await expectMetricValue("Repositories", "1");
    await expectMetricValue("EF Core entities", "1");
  });

  test("WHEN graph loading fails THEN it shows unavailable values for every dashboard metric", async () => {
    server.use(
      http.get("/api/graph", () =>
        HttpResponse.text("no graph available", { status: 500 }),
      ),
    );

    renderWithProviders(<App />);

    await expectMetricValue("Endpoints", "-");
    await expectMetricValue("Controllers", "-");
    await expectMetricValue("Services", "-");
    await expectMetricValue("Repositories", "-");
    await expectMetricValue("EF Core entities", "-");
  });
});
