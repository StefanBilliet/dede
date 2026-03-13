import { Factory } from "fishery";
import type { DashboardMetric } from "../../metrics/dashboardMetrics";

export const dashboardMetricFactory = Factory.define<DashboardMetric>(
  ({ sequence }) => ({
    id: `metric-${sequence}`,
    label: `Metric ${sequence}`,
    value: 0,
  }),
);
