import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MantineProvider } from "@mantine/core";
import { render, screen, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { http, HttpResponse } from "msw";
import { setupServer } from "msw/node";
import App from "./App";
import { graphDocumentFactory } from "./test/builders/graphDocumentFactory";

const server = setupServer();

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

async function expectMetricValue(label: string, value: string) {
  const metric = await screen.findByRole("group", { name: label });
  expect(await within(metric).findByText(value)).toBeInTheDocument();
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
  test("WHEN graph loads with no dashboard node types THEN it shows 0 for every dashboard metric", async () => {
    server.use(
      http.get("/api/graph", () =>
        HttpResponse.json(graphDocumentFactory.build()),
      ),
    );

    renderWithProviders(<App />);

    await expectMetricValue("Endpoints", "0");
    await expectMetricValue("Controllers", "0");
    await expectMetricValue("Services", "0");
    await expectMetricValue("Repositories", "0");
    await expectMetricValue("EF Core entities", "0");
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
