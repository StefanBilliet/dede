import { tmpdir } from "node:os";
import { join } from "node:path";
import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    execArgv: [
      `--localstorage-file=${join(tmpdir(), `dede-vitest-${process.pid}.json`)}`,
    ],
  },
});
