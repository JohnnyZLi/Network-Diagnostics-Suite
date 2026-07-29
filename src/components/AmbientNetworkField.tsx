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
            <feGaussianBlur stdDeviation="3.5" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        <g className="ambient-network-field__page-network">
          <path id="ambient-page-1" d="M 1180 740 L 1320 890 L 1170 1060 L 1360 1220" />
          <path id="ambient-page-2" d="M 30 1020 L 190 920 L 330 1090 L 180 1280 L 360 1430" />
          <path id="ambient-page-3" d="M 1090 1450 L 1260 1330 L 1390 1490 L 1220 1650 L 1410 1810" />
          <path id="ambient-page-4" d="M -20 1760 L 150 1640 L 300 1810 L 120 1980 L 350 2140" />
          <path id="ambient-page-5" d="M 930 2070 L 1110 1930 L 1280 2100 L 1140 2290 L 1410 2430" />

          <g className="ambient-network-field__nodes">
            <circle cx="1180" cy="740" r="3" /><circle cx="1320" cy="890" r="3" /><circle cx="1170" cy="1060" r="3" /><circle cx="1360" cy="1220" r="3" />
            <circle cx="30" cy="1020" r="3" /><circle cx="190" cy="920" r="3" /><circle cx="330" cy="1090" r="3" /><circle cx="180" cy="1280" r="3" /><circle cx="360" cy="1430" r="3" />
            <circle cx="1090" cy="1450" r="3" /><circle cx="1260" cy="1330" r="3" /><circle cx="1390" cy="1490" r="3" /><circle cx="1220" cy="1650" r="3" /><circle cx="1410" cy="1810" r="3" />
            <circle cx="150" cy="1640" r="3" /><circle cx="300" cy="1810" r="3" /><circle cx="120" cy="1980" r="3" /><circle cx="350" cy="2140" r="3" />
            <circle cx="930" cy="2070" r="3" /><circle cx="1110" cy="1930" r="3" /><circle cx="1280" cy="2100" r="3" /><circle cx="1140" cy="2290" r="3" />
          </g>
        </g>

        <g className="ambient-network-field__hero-network">
          <path id="ambient-hero-1" d="M -80 250 L 130 180 L 310 265 L 500 130 L 690 220 L 900 105 L 1110 185" />
          <path id="ambient-hero-2" d="M 40 500 L 215 390 L 390 455 L 570 335 L 755 410 L 955 260 L 1190 330 L 1470 205" />
          <path id="ambient-hero-3" d="M 290 20 L 445 115 L 620 65 L 805 155 L 995 90 L 1190 185 L 1480 125" />
          <path id="ambient-hero-4" d="M -30 665 L 190 575 L 370 655 L 555 530 L 750 590 L 970 455" />
          <path id="ambient-hero-5" d="M 1040 455 L 1195 385 L 1330 500 L 1480 420" />

          <g className="ambient-network-field__nodes">
            <circle cx="130" cy="180" r="3.2" /><circle cx="310" cy="265" r="3.2" /><circle cx="500" cy="130" r="3.2" /><circle cx="690" cy="220" r="3.2" /><circle cx="900" cy="105" r="3.2" /><circle cx="1110" cy="185" r="3.2" />
            <circle cx="215" cy="390" r="3.2" /><circle cx="390" cy="455" r="3.2" /><circle cx="570" cy="335" r="3.2" /><circle cx="755" cy="410" r="3.2" /><circle cx="955" cy="260" r="3.2" /><circle cx="1190" cy="330" r="3.2" />
            <circle cx="445" cy="115" r="3.2" /><circle cx="620" cy="65" r="3.2" /><circle cx="805" cy="155" r="3.2" /><circle cx="995" cy="90" r="3.2" /><circle cx="1190" cy="185" r="3.2" />
            <circle cx="190" cy="575" r="3.2" /><circle cx="370" cy="655" r="3.2" /><circle cx="555" cy="530" r="3.2" /><circle cx="750" cy="590" r="3.2" /><circle cx="970" cy="455" r="3.2" />
            <circle cx="1040" cy="455" r="3.2" /><circle cx="1195" cy="385" r="3.2" /><circle cx="1330" cy="500" r="3.2" />
          </g>
        </g>

        <g className="ambient-network-field__packets" filter="url(#ambient-network-packet-glow)">
          <circle className="ambient-network-field__packet" r="4">
            <animateMotion dur="11s" begin="-2.5s" repeatCount="indefinite">
              <mpath href="#ambient-hero-1" />
            </animateMotion>
            <animate attributeName="opacity" values="0;0;0.9;0.9;0" keyTimes="0;0.08;0.18;0.82;1" dur="11s" begin="-2.5s" repeatCount="indefinite" />
          </circle>
          <circle className="ambient-network-field__packet" r="3.5">
            <animateMotion dur="14s" begin="-8s" repeatCount="indefinite">
              <mpath href="#ambient-hero-2" />
            </animateMotion>
            <animate attributeName="opacity" values="0;0;0.75;0.75;0" keyTimes="0;0.12;0.22;0.8;1" dur="14s" begin="-8s" repeatCount="indefinite" />
          </circle>
          <circle className="ambient-network-field__packet" r="3.5">
            <animateMotion dur="16s" begin="-4s" repeatCount="indefinite">
              <mpath href="#ambient-hero-3" />
            </animateMotion>
            <animate attributeName="opacity" values="0;0;0.65;0.65;0" keyTimes="0;0.16;0.25;0.78;1" dur="16s" begin="-4s" repeatCount="indefinite" />
          </circle>
        </g>
      </svg>
    </div>
  );
}
