import { MantineProvider } from "@mantine/core";
import { render, screen, within } from "@testing-library/react";
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
  expect(within(metric).getByText(value)).toBeInTheDocument();
}

describe("App", () => {
  test("WHEN graph loads with no dashboard node types THEN it shows 0 for every dashboard metric", async () => {
    server.use(
      http.get("/api/graph", () =>
        HttpResponse.json(graphDocumentFactory.build()),
      ),
    );

    render(
      <MantineProvider>
        <App />
      </MantineProvider>,
    );

    await expectMetricValue("Endpoints", "0");
    await expectMetricValue("Controllers", "0");
    await expectMetricValue("Services", "0");
    await expectMetricValue("Repositories", "0");
    await expectMetricValue("EF Core entities", "0");
  });
});
