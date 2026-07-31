import { useEffect, useRef, useState } from "react";
import { formatLatency, formatRate } from "./core/format";
import { clearRecentResults, loadRecentResults, MAX_RECENT_RESULTS, saveRecentResult } from "./core/result-history";
import { runDiagnosticTest, TestCancelledError } from "./diagnostics/run-test";
import { OWNED_SITES, installHeaderMenu, installSiteSwitcher } from "./design-system/site-controls.js";
import { InformationPanels } from "./components/InformationPanels";
import { DeepProbePanel } from "./components/DeepProbePanel";
import { FlowComparisonPanel } from "./components/FlowComparisonPanel";
import { MotionObserver } from "./components/MotionObserver";
import { ProgressStage } from "./components/ProgressStage";
import { RecentResultsPanel } from "./components/RecentResultsPanel";
import { ResultDashboard } from "./components/ResultDashboard";
import { TestControls } from "./components/TestControls";
import type { DiagnosticResult, DownloadPathPreference, TestMode, TestProgress, TransferMode } from "./types/diagnostics";

type RunState = "idle" | "running" | "complete" | "error";

const INITIAL_PROGRESS: TestProgress = {
  phase: "idle",
  fraction: 0,
  bytesTransferred: 0
};

function createResultSummary(result: DiagnosticResult): string {
  const downloadPath = result.download.delivery?.selectedPath ?? "unknown";
  const sampleDetail = result.download.samples && result.download.samples.length > 1
    ? ` (${result.download.samples.length}-sample median)`
    : "";
  const single = result.flowMeasurements?.find((measurement) => measurement.strategy === "single");
  const aggregate = result.flowMeasurements?.find((measurement) => measurement.strategy === "aggregate");
  const lines = [
    "Network Diagnostics Suite",
    `Transfer method: ${result.transferMode ?? "aggregate"}`,
    `Download: ${formatRate(result.download.steadyMbps)} Mbps steady${sampleDetail} (${formatRate(result.download.mbps)} Mbps whole phase)`,
    `Download path: ${downloadPath}`,
    `Upload: ${formatRate(result.upload.steadyMbps)} Mbps steady (${formatRate(result.upload.mbps)} Mbps whole phase)`,
    `Idle latency: ${formatLatency(result.idleLatency.medianMs)} ms median`,
    `Jitter: ${formatLatency(result.idleLatency.jitterMs)} ms`,
    `Request loss: ${result.idleLatency.lossPercent.toFixed(1)}%`,
    `Loaded latency: +${formatLatency(result.downloadLatency.increaseMs)} ms down / +${formatLatency(result.uploadLatency.increaseMs)} ms up`,
    `Network: ${result.edge?.network ?? "Unavailable"}`
  ];
  if (single?.download && aggregate?.download) {
    lines.splice(3, 0,
      `Single connection: ${formatRate(single.download.steadyMbps)} Mbps`,
      `Aggregate capacity: ${formatRate(aggregate.download.steadyMbps)} Mbps`
    );
  }
  return lines.join("\n");
}

function downloadResultFile(result: DiagnosticResult): void {
  const blob = new Blob([JSON.stringify(result, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `network-report-${result.startedAt.replaceAll(":", "-")}.json`;
  anchor.click();
  URL.revokeObjectURL(url);
}

export default function App() {
  const [mode, setMode] = useState<TestMode>("quick");
  const [transferMode, setTransferMode] = useState<TransferMode>("compare");
  const [downloadPath, setDownloadPath] = useState<DownloadPathPreference>("auto");
  const [runState, setRunState] = useState<RunState>("idle");
  const [progress, setProgress] = useState<TestProgress>(INITIAL_PROGRESS);
  const [result, setResult] = useState<DiagnosticResult | null>(null);
  const [history, setHistory] = useState<DiagnosticResult[]>(() => loadRecentResults());
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const [sitesOpen, setSitesOpen] = useState(false);
  const [copyLabel, setCopyLabel] = useState("Copy summary");
  const controllerRef = useRef<AbortController | null>(null);
  const headerRef = useRef<HTMLElement | null>(null);
  const siteSwitcherRef = useRef<HTMLDivElement | null>(null);
  const siteSwitcherControllerRef = useRef<ReturnType<typeof installSiteSwitcher> | null>(null);
  const mobileNavControllerRef = useRef<ReturnType<typeof installHeaderMenu> | null>(null);

  useEffect(() => () => controllerRef.current?.abort("page-unmounted"), []);

  useEffect(() => {
    const header = headerRef.current;
    const siteSwitcher = siteSwitcherRef.current;
    if (!header || !siteSwitcher) return;

    const siteController = installSiteSwitcher(siteSwitcher, {
      onBeforeOpen: () => mobileNavControllerRef.current?.close(),
      onOpenChange: setSitesOpen,
    });
    const navigationController = installHeaderMenu(header, {
      onBeforeOpen: () => siteController.close(),
      onOpenChange: setMobileNavOpen,
    });
    siteSwitcherControllerRef.current = siteController;
    mobileNavControllerRef.current = navigationController;

    return () => {
      siteController.destroy();
      navigationController.destroy();
      siteSwitcherControllerRef.current = null;
      mobileNavControllerRef.current = null;
    };
  }, []);

  const startTest = async () => {
    controllerRef.current?.abort("new-test");
    const controller = new AbortController();
    controllerRef.current = controller;
    setRunState("running");
    setResult(null);
    setErrorMessage(null);
    setProgress(INITIAL_PROGRESS);

    try {
      const nextResult = await runDiagnosticTest({
        mode,
        transferMode,
        downloadPath,
        signal: controller.signal,
        onProgress: (next) => setProgress((previous) => {
          if (next.phase !== previous.phase) return next;
          return {
            phase: next.phase,
            fraction: Math.max(previous.fraction, next.fraction),
            liveMbps: next.liveMbps ?? previous.liveMbps,
            liveLatencyMs: next.liveLatencyMs ?? previous.liveLatencyMs,
            bytesTransferred: Math.max(previous.bytesTransferred, next.bytesTransferred)
          };
        })
      });
      setResult(nextResult);
      setHistory(saveRecentResult(nextResult));
      setRunState("complete");
    } catch (error) {
      if (error instanceof TestCancelledError || controller.signal.aborted) {
        setRunState("idle");
        setProgress(INITIAL_PROGRESS);
      } else {
        setErrorMessage(error instanceof Error ? error.message : "The diagnostic test could not be completed.");
        setRunState("error");
      }
    } finally {
      if (controllerRef.current === controller) controllerRef.current = null;
    }
  };

  const cancelTest = () => controllerRef.current?.abort("cancelled-by-user");

  const exportResult = () => {
    if (result) downloadResultFile(result);
  };

  const copyResult = async () => {
    if (!result) return;
    await navigator.clipboard.writeText(createResultSummary(result));
    setCopyLabel("Copied");
    window.setTimeout(() => setCopyLabel("Copy summary"), 1_500);
  };

  const openSavedResult = (saved: DiagnosticResult) => {
    setResult(saved);
    setRunState("complete");
    setErrorMessage(null);
    setCopyLabel("Copy summary");
  };

  const clearHistory = () => {
    clearRecentResults();
    setHistory([]);
  };

  return (
    <>
      <MotionObserver />
      <div className="app-shell">
        <header ref={headerRef} className="site-header jl-global-header">
          <div className="jl-global-header__inner">
            <div className="wordmark jl-site-identity" aria-label="Johnny Li, Network Diagnostics">
              <a className="jl-site-identity__owner" href="https://johnnyli.dev">Johnny Li</a>
              <span className="jl-site-identity__separator" aria-hidden="true">/</span>
              <a className="wordmark__product jl-site-identity__product" href="/" aria-current="page">Network Diagnostics</a>
            </div>
            <nav
              className={`site-nav jl-global-header__nav jl-header-menu${mobileNavOpen ? " jl-header-menu--open" : ""}`}
              id="primary-navigation"
              aria-label="Primary navigation"
              data-header-menu
            >
              <a href="#methodology">Methodology</a>
              <a href="#privacy">Privacy</a>
              <a href="https://github.com/JohnnyZLi/Network-Diagnostics-Suite" target="_blank" rel="noreferrer">Source <span aria-hidden="true">↗</span></a>
            </nav>
            <div className="header-actions jl-global-header__actions">
              <button
                className="nav-toggle jl-header-menu-toggle"
                type="button"
                aria-expanded={mobileNavOpen}
                aria-controls="primary-navigation"
                data-header-menu-button
              >
                {mobileNavOpen ? "Close" : "Menu"}
              </button>
              <div className="jl-site-switcher" ref={siteSwitcherRef} data-site-switcher>
                <button
                  className="jl-site-switcher__button"
                  type="button"
                  aria-expanded={sitesOpen}
                  aria-controls="owned-sites-menu"
                  data-site-switcher-button
                >
                  <span>Sites</span>
                  <span aria-hidden="true">⌄</span>
                </button>
                <ul
                  className="jl-site-menu"
                  id="owned-sites-menu"
                  hidden={!sitesOpen}
                  data-site-switcher-menu
                >
                  {OWNED_SITES.map((site) => (
                    <li key={site.id}>
                      <a href={site.href} aria-current={site.id === "network" ? "page" : undefined}>{site.label}</a>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          </div>
        </header>

        <main>
          <section className="hero">
            <div className="hero__copy">
              <span className="eyebrow">Browser test + local deep probe</span>
              <h1>Measure the connection,<br /><em>not just the headline speed.</em></h1>
              <p>Throughput is only one part of a usable network. Compare single-flow and aggregate capacity, then inspect latency distributions, jitter, request failures, loaded responsiveness, bufferbloat, and common-service reachability without creating an account.</p>
              <div className="hero__facts">
                <div><strong>1 to 10</strong><span>connections</span></div>
                <div><strong>6</strong><span>service targets</span></div>
                <div><strong>{MAX_RECENT_RESULTS}</strong><span>local reports</span></div>
              </div>
            </div>

            <TestControls
              mode={mode}
              transferMode={transferMode}
              downloadPath={downloadPath}
              running={runState === "running"}
              onModeChange={setMode}
              onTransferModeChange={setTransferMode}
              onDownloadPathChange={setDownloadPath}
              onStart={startTest}
              onCancel={cancelTest}
            />
          </section>

          {runState === "running" && <ProgressStage progress={progress} />}

          {runState === "error" && (
            <section className="error-panel jl-callout jl-callout--danger" role="alert">
              <span>Test interrupted</span>
              <h2>The measurement endpoint did not finish the request.</h2>
              <p>{errorMessage} Check the connection and try again.</p>
              <button className="jl-button jl-button--compact" type="button" onClick={startTest}>Try again</button>
            </section>
          )}

          {result && (
            <>
              <ResultDashboard
                result={result}
                onExport={exportResult}
                onCopy={copyResult}
                copyLabel={copyLabel}
              />
              <FlowComparisonPanel result={result} />
            </>
          )}

          {!result && runState !== "running" && runState !== "error" && (
            <section className="measurement-preview" aria-label="Available measurements">
              <div className="section-heading">
                <span className="eyebrow">Beyond a single number</span>
                <h2>One run, <em className="text-blue">two throughput views.</em></h2>
                <p>Compare mode measures an isolated connection and aggregate capacity separately, while latency is sampled at rest and under transfer load.</p>
              </div>
              <div className="preview-grid">
                <article><span>01 / Single</span><h3>Individual transfer behavior</h3><p>One connection shows what a single download, tunnel, or remote service can use on the selected path.</p></article>
                <article><span>02 / Aggregate</span><h3>Total application capacity</h3><p>Parallel connections show how much throughput several simultaneous transfers can use together.</p></article>
                <article><span>03 / Loaded</span><h3>Queue pressure</h3><p>Latency during download and upload reveals responsiveness problems that an unloaded ping cannot show.</p></article>
              </div>
            </section>
          )}

          <RecentResultsPanel
            results={history}
            currentResultId={result?.id ?? null}
            onOpen={openSavedResult}
            onExport={downloadResultFile}
            onClear={clearHistory}
          />

          <section className="methodology" id="methodology">
            <div className="section-heading">
              <span className="eyebrow">Measurement contract</span>
              <h2>Every number says <em className="text-violet">what it actually measured.</em></h2>
            </div>
            <div className="methodology-grid">
              <article><span>HTTP</span><h3>Browser request loss</h3><p>A failed or timed-out request. Transmission Control Protocol can retransmit underlying packets, so this is deliberately not labeled raw packet loss.</p></article>
              <article><span>RTT</span><h3>Round-trip latency</h3><p>High-resolution elapsed time for an uncached request to the nearest configured test edge, summarized as a distribution.</p></article>
              <article><span>LOAD</span><h3>Bufferbloat signal</h3><p>The change between idle median latency and the median observed during each saturated transfer stage.</p></article>
              <article><span>RATE</span><h3>Application throughput</h3><p>Single and aggregate phases are isolated from each other. Compare mode reports both; Stress adds a connection-scaling curve.</p></article>
            </div>
          </section>

          <DeepProbePanel />
          <div id="privacy"><InformationPanels /></div>
        </main>

        <footer>
          <span>Network Diagnostics Suite</span>
          <p>Open source · no analytics · no accounts · local-only report history</p>
          <a href="https://johnnyli.dev">Back to johnnyli.dev</a>
        </footer>
      </div>
    </>
  );
}
