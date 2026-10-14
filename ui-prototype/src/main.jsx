import React from "react";
import { createRoot } from "react-dom/client";
import { LiveWorkspace } from "./LiveWorkspace.tsx";
import { ErrorBoundary } from "./ErrorBoundary.tsx";
import "./styles.css";

createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <ErrorBoundary><LiveWorkspace /></ErrorBoundary>
  </React.StrictMode>,
);
