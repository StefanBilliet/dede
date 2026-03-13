import { Paper, SimpleGrid, Text } from "@mantine/core";
import * as React from "react";
import type { DashboardMetric } from "./dashboardMetrics";

export const Metrics: React.FC<{ metrics: DashboardMetric[] }> = ({ metrics }) => {
  return (
    <SimpleGrid cols={{ base: 1, sm: 2, lg: 5 }} spacing="md">
      {metrics.map((metric) => {
        const titleId = `metric-${metric.id}`;

        return (
          <Paper
            key={metric.id}
            shadow="sm"
            radius="lg"
            withBorder
            className="metric-card"
            role="group"
            aria-labelledby={titleId}
          >
            <dl>
              <Text component="dt" id={titleId} size="xs" tt="uppercase" fw={700} c="dimmed">
                {metric.label}
              </Text>
              <Text component="dd" className="metric-value">
                {metric.value}
              </Text>
            </dl>
          </Paper>
        );
      })}
    </SimpleGrid>
  );
};
