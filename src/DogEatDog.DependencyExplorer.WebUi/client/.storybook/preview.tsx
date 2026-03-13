import { MantineProvider } from "@mantine/core";
import type { Preview } from "@storybook/react";
import "@mantine/core/styles.css";
import "@mantine/notifications/styles.css";
import "../src/index.css";

const preview: Preview = {
  decorators: [
    (Story) => (
      <MantineProvider defaultColorScheme="light">
        <div style={{ minHeight: "100vh", padding: "1rem" }}>
          <Story />
        </div>
      </MantineProvider>
    ),
  ],
  parameters: {
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
    layout: "fullscreen",
  },
};

export default preview;
