import { MantineProvider } from "@mantine/core";
import { render, screen, within } from "@testing-library/react";
import { dashboardMetricFactory } from "../test/builders/dashboardMetricFactory";
import type { DashboardMetric } from "./dashboardMetrics";
import { Metrics } from "./metrics";

async function expectMetricValue(label: string, value: string) {
  const metric = await screen.findByRole("group", { name: label });
  expect(await within(metric).findByText(value)).toBeInTheDocument();
}

function renderMetrics(metrics: DashboardMetric[]) {
  render(
    <MantineProvider>
      <Metrics metrics={metrics} />
    </MantineProvider>,
  );
}

const cases = [
  {
    name: "WHEN metric values are 0 THEN it renders them accessibly",
    metrics: [
      dashboardMetricFactory.build({
        id: "endpoints",
        label: "Endpoints",
        value: 0,
      }),
      dashboardMetricFactory.build({
        id: "controllers",
        label: "Controllers",
        value: 0,
      }),
      dashboardMetricFactory.build({
        id: "services",
        label: "Services",
        value: 0,
      }),
      dashboardMetricFactory.build({
        id: "repositories",
        label: "Repositories",
        value: 0,
      }),
      dashboardMetricFactory.build({
        id: "entities",
        label: "EF Core entities",
        value: 0,
      }),
    ],
    expectedValue: "0",
  },
  {
    name: "WHEN metric values are positive THEN it renders them accessibly",
    metrics: [
      dashboardMetricFactory.build({
        id: "endpoints",
        label: "Endpoints",
        value: 1,
      }),
      dashboardMetricFactory.build({
        id: "controllers",
        label: "Controllers",
        value: 1,
      }),
      dashboardMetricFactory.build({
        id: "services",
        label: "Services",
        value: 1,
      }),
      dashboardMetricFactory.build({
        id: "repositories",
        label: "Repositories",
        value: 1,
      }),
      dashboardMetricFactory.build({
        id: "entities",
        label: "EF Core entities",
        value: 1,
      }),
    ],
    expectedValue: "1",
  },
  {
    name: "WHEN metric values are unavailable THEN it renders placeholders accessibly",
    metrics: [
      dashboardMetricFactory.build({
        id: "endpoints",
        label: "Endpoints",
        value: "-",
      }),
      dashboardMetricFactory.build({
        id: "controllers",
        label: "Controllers",
        value: "-",
      }),
      dashboardMetricFactory.build({
        id: "services",
        label: "Services",
        value: "-",
      }),
      dashboardMetricFactory.build({
        id: "repositories",
        label: "Repositories",
        value: "-",
      }),
      dashboardMetricFactory.build({
        id: "entities",
        label: "EF Core entities",
        value: "-",
      }),
    ],
    expectedValue: "-",
  },
] satisfies Array<{
  name: string;
  metrics: DashboardMetric[];
  expectedValue: string;
}>;

describe("Metrics", () => {
  test.each(cases)("$name", async ({ metrics, expectedValue }) => {
    renderMetrics(metrics);

    await expectMetricValue("Endpoints", expectedValue);
    await expectMetricValue("Controllers", expectedValue);
    await expectMetricValue("Services", expectedValue);
    await expectMetricValue("Repositories", expectedValue);
    await expectMetricValue("EF Core entities", expectedValue);
  });
});
