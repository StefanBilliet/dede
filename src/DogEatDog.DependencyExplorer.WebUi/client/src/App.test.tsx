import { MantineProvider } from "@mantine/core";
import { render, screen } from "@testing-library/react";
import App from "./App";

describe("App", () => {
  it("renders scaffold heading", () => {
    render(
      <MantineProvider>
        <App />
      </MantineProvider>,
    );

    expect(
      screen.getByRole("heading", { name: "dede React UI" }),
    ).toBeInTheDocument();
  });
});
