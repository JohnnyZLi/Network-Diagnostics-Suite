import "./HeroSignalTrace.css";

const TRACE_POINTS = [
  "8,176",
  "76,164",
  "118,199",
  "164,140",
  "214,165",
  "258,100",
  "302,136",
  "348,82",
  "394,128",
  "444,62",
  "496,148",
  "548,112",
  "602,174",
  "654,98",
  "706,138",
  "756,55",
  "806,95",
  "854,38",
  "902,77",
  "930,65"
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
            <circle cx="258" cy="100" r="3" />
            <circle cx="444" cy="62" r="3" />
            <circle cx="654" cy="98" r="3" />
            <circle cx="854" cy="38" r="3" />
          </g>

          <circle className="hero-signal__hinge" cx="930" cy="65" r="4" />
          <g className="hero-signal__live-tip">
            <line
              x1="930"
              y1="65"
              x2="982"
              y2="29"
              markerEnd="url(#hero-signal-live-arrow)"
            />
          </g>
        </g>
      </svg>
    </div>
  );
}
