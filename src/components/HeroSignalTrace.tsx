import "./HeroSignalTrace.css";

const TRACE_POINTS = [
  "8,226",
  "76,219",
  "118,239",
  "164,194",
  "214,209",
  "258,151",
  "302,178",
  "348,126",
  "394,164",
  "444,111",
  "496,186",
  "548,151",
  "602,204",
  "654,143",
  "706,172",
  "756,102",
  "806,129",
  "854,73",
  "902,99",
  "930,90"
].join(" ");

export function HeroSignalTrace() {
  return (
    <div className="hero-signal" aria-hidden="true">
      <svg
        className="hero-signal__svg"
        viewBox="0 0 1000 280"
        preserveAspectRatio="none"
        focusable="false"
      >
        <defs>
          <linearGradient id="hero-signal-gradient" x1="0" y1="0" x2="1" y2="0">
            <stop offset="0" stopColor="currentColor" stopOpacity="0" />
            <stop offset="0.16" stopColor="currentColor" stopOpacity="0.22" />
            <stop offset="0.58" stopColor="currentColor" stopOpacity="0.46" />
            <stop offset="1" stopColor="currentColor" stopOpacity="0.72" />
          </linearGradient>
          <filter id="hero-signal-glow" x="-20%" y="-40%" width="140%" height="180%">
            <feGaussianBlur stdDeviation="2.4" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
          <marker
            id="hero-signal-live-arrow"
            markerWidth="12"
            markerHeight="12"
            refX="10"
            refY="6"
            orient="auto"
            markerUnits="strokeWidth"
          >
            <path d="M 0 0 L 12 6 L 0 12 Z" fill="currentColor" />
          </marker>
        </defs>

        <g className="hero-signal__field" filter="url(#hero-signal-glow)">
          <polyline points={TRACE_POINTS} className="hero-signal__path hero-signal__path--base" />
          <polyline points={TRACE_POINTS} className="hero-signal__path hero-signal__path--trace" />

          <g className="hero-signal__samples">
            <circle cx="258" cy="151" r="3" />
            <circle cx="444" cy="111" r="3" />
            <circle cx="654" cy="143" r="3" />
            <circle cx="854" cy="73" r="3" />
          </g>

          <circle className="hero-signal__hinge" cx="930" cy="90" r="4" />
          <g className="hero-signal__live-tip">
            <line
              x1="930"
              y1="90"
              x2="982"
              y2="54"
              markerEnd="url(#hero-signal-live-arrow)"
            />
          </g>
        </g>
      </svg>
    </div>
  );
}
