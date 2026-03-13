import { Badge, Paper, Stack, Text, Title } from "@mantine/core";
import type { Meta, StoryObj } from "@storybook/react";

function ScaffoldStatus() {
  return (
    <Paper radius="lg" p="lg" withBorder maw={560}>
      <Stack gap="sm">
        <Badge color="teal" variant="light" w="fit-content">
          React scaffold ready
        </Badge>
        <Title order={2}>dede UI modernization</Title>
        <Text c="dimmed">
          This Storybook setup runs alongside the legacy UI and previews new
          Mantine components.
        </Text>
      </Stack>
    </Paper>
  );
}

const meta = {
  title: "Scaffold/Status",
  component: ScaffoldStatus,
} satisfies Meta<typeof ScaffoldStatus>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
