import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
import { installCompactNavigationEscape } from "./compact-navigation-escape";
import "./design-system/tokens.css";
import "./design-system/foundations.css";
import "./design-system/site-identity.css";
import "./styles.css";
import "./history.css";
import "./report-details.css";
import "./ui-polish.css";
import "./metric-card-layout.css";
import "./test-controls.css";
import "./transfer-color.css";
import "./full-bleed-layout.css";
import "./design-system-adapter.css";

installCompactNavigationEscape();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
