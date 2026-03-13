import type { Meta, StoryObj } from "@storybook/react";
import {
  createUnavailableMetrics,
  type DashboardMetric,
} from "../metrics/dashboardMetrics";
import { Metrics } from "../metrics/metrics";

const metricStates = {
  Populated: [
    { label: "Endpoints", value: 14 },
    { label: "Controllers", value: 7 },
    { label: "Services", value: 23 },
    { label: "Repositories", value: 11 },
    { label: "EF Core entities", value: 48 },
  ],
  Unavailable: createUnavailableMetrics(),
  Empty: [
    { label: "Endpoints", value: 0 },
    { label: "Controllers", value: 0 },
    { label: "Services", value: 0 },
    { label: "Repositories", value: 0 },
    { label: "EF Core entities", value: 0 },
  ],
  LargeValues: [
    { label: "Endpoints", value: 1250 },
    { label: "Controllers", value: 620 },
    { label: "Services", value: 3875 },
    { label: "Repositories", value: 1420 },
    { label: "EF Core entities", value: 12045 },
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
