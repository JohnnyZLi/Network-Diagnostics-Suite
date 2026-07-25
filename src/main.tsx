import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
import "./styles.css";
import "./history.css";
import "./report-details.css";
import "./ui-polish.css";
import "./transfer-color.css";
import "./full-bleed-layout.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>
);
