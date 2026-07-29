import "./AmbientNetworkField.css";

export function AmbientNetworkField() {
  return (
    <div className="ambient-network-field" aria-hidden="true">
      <svg
        className="ambient-network-field__canvas"
        viewBox="0 0 1440 2600"
        preserveAspectRatio="none"
        focusable="false"
      >
        <defs>
          <filter id="ambient-network-packet-glow" x="-300%" y="-300%" width="700%" height="700%">
            <feGaussianBlur stdDeviation="3" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        <g className="ambient-network-field__page-network">
          <path d="M 1180 620 L 1320 700 L 1210 805 L 1390 900" />
          <path d="M -20 930 L 120 865 L 245 970 L 125 1080 L 310 1160" />
          <path d="M 1130 1300 L 1270 1210 L 1400 1325 L 1260 1440 L 1450 1540" />
          <path d="M -30 1750 L 115 1665 L 255 1780 L 120 1910 L 305 2020" />
          <path d="M 1120 2160 L 1260 2070 L 1390 2180 L 1250 2320 L 1460 2440" />

          <g className="ambient-network-field__nodes">
            <circle cx="1180" cy="620" r="2.7" /><circle cx="1320" cy="700" r="2.7" /><circle cx="1210" cy="805" r="2.7" /><circle cx="1390" cy="900" r="2.7" />
            <circle cx="120" cy="865" r="2.7" /><circle cx="245" cy="970" r="2.7" /><circle cx="125" cy="1080" r="2.7" /><circle cx="310" cy="1160" r="2.7" />
            <circle cx="1130" cy="1300" r="2.7" /><circle cx="1270" cy="1210" r="2.7" /><circle cx="1400" cy="1325" r="2.7" /><circle cx="1260" cy="1440" r="2.7" />
            <circle cx="115" cy="1665" r="2.7" /><circle cx="255" cy="1780" r="2.7" /><circle cx="120" cy="1910" r="2.7" /><circle cx="305" cy="2020" r="2.7" />
            <circle cx="1120" cy="2160" r="2.7" /><circle cx="1260" cy="2070" r="2.7" /><circle cx="1390" cy="2180" r="2.7" /><circle cx="1250" cy="2320" r="2.7" />
          </g>
        </g>

        <g className="ambient-network-field__hero-network">
          <path id="ambient-hero-right" d="M 980 78 L 1100 125 L 1215 72 L 1335 138 L 1475 100" />
          <path id="ambient-hero-left" d="M -70 350 L 75 315 L 215 365 L 360 305 L 510 358 L 670 322" />
          <path id="ambient-hero-lower-right" d="M 805 365 L 930 315 L 1065 360 L 1205 302 L 1350 352 L 1490 318" />

          <g className="ambient-network-field__nodes">
            <circle cx="980" cy="78" r="3" /><circle cx="1100" cy="125" r="3" /><circle cx="1215" cy="72" r="3" /><circle cx="1335" cy="138" r="3" />
            <circle cx="75" cy="315" r="3" /><circle cx="215" cy="365" r="3" /><circle cx="360" cy="305" r="3" /><circle cx="510" cy="358" r="3" /><circle cx="670" cy="322" r="3" />
            <circle cx="805" cy="365" r="3" /><circle cx="930" cy="315" r="3" /><circle cx="1065" cy="360" r="3" /><circle cx="1205" cy="302" r="3" /><circle cx="1350" cy="352" r="3" />
          </g>
        </g>

        <g className="ambient-network-field__packets" filter="url(#ambient-network-packet-glow)">
          <circle className="ambient-network-field__packet" r="3.7">
            <animateMotion dur="13s" begin="-3s" repeatCount="indefinite">
              <mpath href="#ambient-hero-left" />
            </animateMotion>
            <animate attributeName="opacity" values="0;0;0.82;0.82;0" keyTimes="0;0.12;0.22;0.8;1" dur="13s" begin="-3s" repeatCount="indefinite" />
          </circle>
          <circle className="ambient-network-field__packet" r="3.3">
            <animateMotion dur="17s" begin="-10s" repeatCount="indefinite">
              <mpath href="#ambient-hero-right" />
            </animateMotion>
            <animate attributeName="opacity" values="0;0;0.62;0.62;0" keyTimes="0;0.18;0.28;0.76;1" dur="17s" begin="-10s" repeatCount="indefinite" />
          </circle>
        </g>
      </svg>
    </div>
  );
}
