import type { Meta, StoryObj } from "@storybook/react";
import {
  createUnavailableMetrics,
  type DashboardMetric,
} from "../metrics/dashboardMetrics";
import { Metrics } from "../metrics/metrics";
import { dashboardMetricFactory } from "../test/builders/dashboardMetricFactory";

const metricStates = {
  Populated: [
    dashboardMetricFactory.build({
      id: "endpoints",
      label: "Endpoints",
      value: 14,
    }),
    dashboardMetricFactory.build({
      id: "controllers",
      label: "Controllers",
      value: 7,
    }),
    dashboardMetricFactory.build({
      id: "services",
      label: "Services",
      value: 23,
    }),
    dashboardMetricFactory.build({
      id: "repositories",
      label: "Repositories",
      value: 11,
    }),
    dashboardMetricFactory.build({
      id: "entities",
      label: "EF Core entities",
      value: 48,
    }),
  ],
  Unavailable: createUnavailableMetrics(),
  Empty: [
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
  LargeValues: [
    dashboardMetricFactory.build({
      id: "endpoints",
      label: "Endpoints",
      value: 1250,
    }),
    dashboardMetricFactory.build({
      id: "controllers",
      label: "Controllers",
      value: 620,
    }),
    dashboardMetricFactory.build({
      id: "services",
      label: "Services",
      value: 3875,
    }),
    dashboardMetricFactory.build({
      id: "repositories",
      label: "Repositories",
      value: 1420,
    }),
    dashboardMetricFactory.build({
      id: "entities",
      label: "EF Core entities",
      value: 12045,
    }),
  ],
} satisfies Record<string, DashboardMetric[]>;

type MetricsStoryProps = {
  state: keyof typeof metricStates;
};

function MetricsStory({ state }: MetricsStoryProps) {
  return <Metrics metrics={metricStates[state]} />;
}

const meta = {
  title: "Metrics/Metrics",
  component: MetricsStory,
  render: (args) => (
    <div style={{ maxWidth: 1120, margin: "0 auto" }}>
      <MetricsStory {...args} />
    </div>
  ),
  argTypes: {
    state: {
      control: { type: "radio" },
      options: Object.keys(metricStates),
    },
  },
  args: {
    state: "Populated",
  },
} satisfies Meta<typeof MetricsStory>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Playground: Story = {};
