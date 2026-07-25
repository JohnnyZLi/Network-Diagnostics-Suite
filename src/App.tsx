import { useEffect, useRef, useState } from "react";
import { formatLatency, formatRate } from "./core/format";
import { clearRecentResults, loadRecentResults, MAX_RECENT_RESULTS, saveRecentResult } from "./core/result-history";
import { runDiagnosticTest, TestCancelledError } from "./diagnostics/run-test";
import { InformationPanels } from "./components/InformationPanels";
import { DeepProbePanel } from "./components/DeepProbePanel";
import { MotionObserver } from "./components/MotionObserver";
import { ProgressStage } from "./components/ProgressStage";
import { RecentResultsPanel } from "./components/RecentResultsPanel";
import { ResultDashboard } from "./components/ResultDashboard";
import { TestControls } from "./components/TestControls";
import type { DiagnosticResult, DownloadPathPreference, TestMode, TestProgress } from "./types/diagnostics";

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
  return [
    "Network Diagnostics Suite",
    `Download: ${formatRate(result.download.steadyMbps)} Mbps steady${sampleDetail} (${formatRate(result.download.mbps)} Mbps whole phase)`,
    `Download path: ${downloadPath}`,
    `Upload: ${formatRate(result.upload.steadyMbps)} Mbps steady (${formatRate(result.upload.mbps)} Mbps whole phase)`,
    `Idle latency: ${formatLatency(result.idleLatency.medianMs)} ms median`,
    `Jitter: ${formatLatency(result.idleLatency.jitterMs)} ms`,
    `Request loss: ${result.idleLatency.lossPercent.toFixed(1)}%`,
    `Loaded latency: +${formatLatency(result.downloadLatency.increaseMs)} ms down / +${formatLatency(result.uploadLatency.increaseMs)} ms up`,
    `Network: ${result.edge?.network ?? "Unavailable"}`
  ].join("\n");
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
  const [downloadPath, setDownloadPath] = useState<DownloadPathPreference>("auto");
  const [runState, setRunState] = useState<RunState>("idle");
  const [progress, setProgress] = useState<TestProgress>(INITIAL_PROGRESS);
  const [result, setResult] = useState<DiagnosticResult | null>(null);
  const [history, setHistory] = useState<DiagnosticResult[]>(() => loadRecentResults());
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [stressConfirmed, setStressConfirmed] = useState(false);
  const [copyLabel, setCopyLabel] = useState("Copy summary");
  const controllerRef = useRef<AbortController | null>(null);

  useEffect(() => () => controllerRef.current?.abort("page-unmounted"), []);

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
      <header className="site-header">
        <a className="wordmark" href="/" aria-label="Network Diagnostics Suite home">
          <span className="wordmark__mark" aria-hidden="true"><i /><i /><i /></span>
          <span>Network Diagnostics</span>
        </a>
        <nav aria-label="Primary navigation">
          <a href="#methodology">Methodology</a>
          <a href="#privacy">Privacy</a>
          <a href="https://github.com/JohnnyZLi" target="_blank" rel="noreferrer">GitHub <span aria-hidden="true">↗</span></a>
        </nav>
        <a className="privacy-status" href="https://johnnyli.dev" aria-label="Return to Johnny Li portfolio">← Portfolio</a>
      </header>

      <main>
        <section className="hero">
          <div className="hero__copy">
            <span className="eyebrow">Browser test + local deep probe</span>
            <h1>Measure the connection,<br /><em>not just the headline speed.</em></h1>
            <p>Throughput is only one part of a usable network. Test latency distributions, jitter, request failures, loaded responsiveness, bufferbloat, and common-service reachability without creating an account. Recent reports stay only in this browser.</p>
            <div className="hero__facts">
              <div><strong>3</strong><span>download samples</span></div>
              <div><strong>6</strong><span>service targets</span></div>
              <div><strong>{MAX_RECENT_RESULTS}</strong><span>local reports</span></div>
            </div>
          </div>

          <TestControls
            mode={mode}
            downloadPath={downloadPath}
            running={runState === "running"}
            stressConfirmed={stressConfirmed}
            onModeChange={(nextMode) => {
              setMode(nextMode);
              if (nextMode !== "extended") setStressConfirmed(false);
            }}
            onDownloadPathChange={setDownloadPath}
            onStressConfirmed={setStressConfirmed}
            onStart={startTest}
            onCancel={cancelTest}
          />
        </section>

        {runState === "running" && <ProgressStage progress={progress} />}

        {runState === "error" && (
          <section className="error-panel" role="alert">
            <span>Test interrupted</span>
            <h2>The measurement endpoint did not finish the request.</h2>
            <p>{errorMessage} Check the connection and try again.</p>
            <button type="button" onClick={startTest}>Try again</button>
          </section>
        )}

        {result && (
          <ResultDashboard
            result={result}
            onExport={exportResult}
            onCopy={copyResult}
            copyLabel={copyLabel}
          />
        )}

        {!result && runState !== "running" && runState !== "error" && (
          <section className="measurement-preview" aria-label="Available measurements">
            <div className="section-heading">
              <span className="eyebrow">Beyond a single number</span>
              <h2>One run, three network conditions.</h2>
              <p>The same latency series is sampled at rest, under download load, and under upload load so queueing delay is visible.</p>
            </div>
            <div className="preview-grid">
              <article><span>01 / Idle</span><h3>Baseline quality</h3><p>Median, mean, minimum, maximum, 95th percentile, jitter, and request timeouts.</p></article>
              <article><span>02 / Downstream</span><h3>Loaded responsiveness</h3><p>Three parallel-stream samples report a median download rate alongside loaded latency and stability.</p></article>
              <article><span>03 / Upstream</span><h3>Queue pressure</h3><p>Upload saturation reveals call and gaming delays that an unloaded ping cannot show.</p></article>
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
            <h2>Every number says what it actually measured.</h2>
          </div>
          <div className="methodology-grid">
            <article><span>HTTP</span><h3>Browser request loss</h3><p>A failed or timed-out request. Transmission Control Protocol can retransmit underlying packets, so this is deliberately not labeled raw packet loss.</p></article>
            <article><span>RTT</span><h3>Round-trip latency</h3><p>High-resolution elapsed time for an uncached request to the nearest configured test edge, summarized as a distribution.</p></article>
            <article><span>LOAD</span><h3>Bufferbloat signal</h3><p>The change between idle median latency and the median observed during each saturated transfer direction.</p></article>
            <article><span>RATE</span><h3>Application throughput</h3><p>The displayed download is the median of three successful payload-rate samples; upload remains one sustained phase.</p></article>
          </div>
        </section>

        <DeepProbePanel />
        <div id="privacy"><InformationPanels /></div>
      </main>

      <footer>
        <span>Network Diagnostics Suite</span>
        <p>Open source · no analytics · no accounts · local-only report history</p>
        <a href="https://johnnyli.dev">Back to johnnyli.dev ↗</a>
      </footer>
      </div>
    </>
  );
}
