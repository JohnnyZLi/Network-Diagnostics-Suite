export function AmbientNetwork() {
  return (
    <div className="ambient-network" aria-hidden="true">
      <div className="ambient-network__glow ambient-network__glow--blue" />
      <div className="ambient-network__glow ambient-network__glow--violet" />

      <svg viewBox="0 0 1600 1000" preserveAspectRatio="xMidYMid slice" focusable="false">
        <defs>
          <linearGradient id="ambient-route-gradient" x1="0" y1="0" x2="1" y2="0">
            <stop offset="0" stopColor="#57b9ff" stopOpacity="0" />
            <stop offset="0.32" stopColor="#7dd3fc" stopOpacity="0.52" />
            <stop offset="0.72" stopColor="#a78bfa" stopOpacity="0.36" />
            <stop offset="1" stopColor="#a78bfa" stopOpacity="0" />
          </linearGradient>
        </defs>

        <g className="ambient-network__routes">
          <path className="ambient-network__route" d="M-120 220 C190 20 420 360 710 180 S1260 30 1720 250" />
          <path className="ambient-network__route" d="M1180 -120 C1040 180 1450 330 1180 610 S860 860 1050 1120" />
          <path className="ambient-network__route" d="M-130 760 C170 580 330 910 620 720 S910 520 1180 790 S1450 920 1720 720" />
          <path className="ambient-network__route ambient-network__route--minor" d="M100 -80 C260 210 80 420 310 590 S650 870 520 1100" />

          <path pathLength="100" className="ambient-network__signal ambient-network__signal--one" d="M-120 220 C190 20 420 360 710 180 S1260 30 1720 250" />
          <path pathLength="100" className="ambient-network__signal ambient-network__signal--two" d="M1180 -120 C1040 180 1450 330 1180 610 S860 860 1050 1120" />
          <path pathLength="100" className="ambient-network__signal ambient-network__signal--three" d="M-130 760 C170 580 330 910 620 720 S910 520 1180 790 S1450 920 1720 720" />
        </g>

        <g className="ambient-network__nodes">
          <circle className="ambient-network__node-ring ambient-network__node--one" cx="250" cy="112" r="13" />
          <circle className="ambient-network__node-core ambient-network__node--one" cx="250" cy="112" r="2.5" />
          <circle className="ambient-network__node-ring ambient-network__node--two" cx="714" cy="178" r="11" />
          <circle className="ambient-network__node-core ambient-network__node--two" cx="714" cy="178" r="2.5" />
          <circle className="ambient-network__node-ring ambient-network__node--three" cx="1260" cy="119" r="14" />
          <circle className="ambient-network__node-core ambient-network__node--three" cx="1260" cy="119" r="2.5" />
          <circle className="ambient-network__node-ring ambient-network__node--four" cx="1184" cy="608" r="12" />
          <circle className="ambient-network__node-core ambient-network__node--four" cx="1184" cy="608" r="2.5" />
          <circle className="ambient-network__node-ring ambient-network__node--five" cx="620" cy="720" r="13" />
          <circle className="ambient-network__node-core ambient-network__node--five" cx="620" cy="720" r="2.5" />
          <circle className="ambient-network__node-ring ambient-network__node--six" cx="312" cy="590" r="10" />
          <circle className="ambient-network__node-core ambient-network__node--six" cx="312" cy="590" r="2.5" />
        </g>

        <circle className="ambient-network__packet" r="2.6">
          <animateMotion dur="19s" begin="-7s" repeatCount="indefinite" path="M-120 220 C190 20 420 360 710 180 S1260 30 1720 250" />
        </circle>
        <circle className="ambient-network__packet ambient-network__packet--violet" r="2.4">
          <animateMotion dur="27s" begin="-16s" repeatCount="indefinite" path="M1180 -120 C1040 180 1450 330 1180 610 S860 860 1050 1120" />
        </circle>
        <circle className="ambient-network__packet" r="2.2">
          <animateMotion dur="32s" begin="-22s" repeatCount="indefinite" path="M-130 760 C170 580 330 910 620 720 S910 520 1180 790 S1450 920 1720 720" />
        </circle>
      </svg>
    </div>
  );
}
