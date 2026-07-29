import "./HeroSignalTrace.css";

const TRACE_PATH = [
  "M 8 204",
  "L 76 188",
  "L 126 216",
  "L 184 154",
  "L 244 178",
  "L 304 112",
  "L 362 146",
  "L 424 92",
  "L 486 132",
  "L 548 72",
  "L 612 150",
  "L 676 108",
  "L 740 136",
  "L 804 66",
  "L 868 102",
  "L 930 52",
  "L 988 76"
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
          <filter id="hero-signal-glow" x="-20%" y="-40%" width="140%" height="180%">
            <feGaussianBlur stdDeviation="2.1" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        <g className="hero-signal__animated" filter="url(#hero-signal-glow)">
          <path
            className="hero-signal__trace"
            d={TRACE_PATH}
            pathLength="1"
          >
            <animate
              attributeName="stroke-dashoffset"
              dur="8s"
              repeatCount="indefinite"
              values="1;1;0;0;0;1"
              keyTimes="0;0.08;0.64;0.82;0.999;1"
              calcMode="linear"
            />
            <animate
              attributeName="opacity"
              dur="8s"
              repeatCount="indefinite"
              values="0;0;0.34;0.34;0;0"
              keyTimes="0;0.08;0.1;0.82;0.92;1"
              calcMode="linear"
            />
          </path>

          <g className="hero-signal__head">
            <circle r="3.2" />
            <path d="M -10 -5 L 3 0 L -10 5 Z" />
            <animate
              attributeName="opacity"
              dur="8s"
              repeatCount="indefinite"
              values="0;0;0.78;0.78;0;0"
              keyTimes="0;0.08;0.1;0.82;0.92;1"
              calcMode="linear"
            />
            <animateMotion
              dur="8s"
              repeatCount="indefinite"
              rotate="auto"
              keyPoints="0;0;1;1;1;0"
              keyTimes="0;0.08;0.64;0.82;0.999;1"
              calcMode="linear"
              path={TRACE_PATH}
            />
          </g>
        </g>

        <path
          className="hero-signal__static"
          d={TRACE_PATH}
          pathLength="1"
        />
      </svg>
    </div>
  );
}
