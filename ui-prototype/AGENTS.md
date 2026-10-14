# Prototype Instructions

Approved WorkPulse direction: `../Obsidian/Источники/ui-concept-v4-minimal.png`. Keep horizontal text navigation; no sidebar. Use light cool mint, dark ink, small flat Tabler icons, compact controls, and an understated editorial illustration of adults. No photos, cartoon faces, oversized decorative components or 3D KPI icons. Interface Russian. For LR9 the user explicitly authorized connecting this UI to the real REST API and PostgreSQL (2026-10-13), with cookie authentication and server-enforced roles. `LiveWorkspace.tsx` is the active entry point; `App.tsx` retains the historical demo. Do not mix fake metrics with server results. User does not need a new LR6 report. Current editable Figma (historical visual reference): https://www.figma.com/design/ANBCWFTYaghJqVXF43wvEr?node-id=6-27.

Run the local server yourself and open the preview in the browser available to this environment. Do not give the user server-start instructions when you can run it.

Before making substantial visual changes, use the Product Design plugin's `get-context` skill when the visual source is unclear or no longer matches the current goal. When the user gives durable prototype-specific design feedback, preferences, or decisions, record them in `AGENTS.md`.

When implementing from a selected generated mock, treat that image as the source of truth for layout, component anatomy, density, spacing, color, typography, visible content, and hierarchy.

Build app UI in `src/`. Keep `.openai/hosting.json`, `worker/index.js`, `scripts/prepare-sites-build.mjs`, and `tests/sites-worker.test.mjs` intact so the same local prototype can be handed to Sites. Before a Sites handoff, run `npm run build` and `npm run test:sites`; the build must leave `dist/client/index.html`, `dist/server/index.js`, and `dist/.openai/hosting.json`.
