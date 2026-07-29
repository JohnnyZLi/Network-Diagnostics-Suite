import { useEffect, useRef } from "react";
import "./HeroSignalTrace.css";

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(maximum, Math.max(minimum, value));
}

export function HeroSignalTrace() {
  const rootRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const root = rootRef.current;
    const hero = root?.closest<HTMLElement>(".hero");
    if (!root || !hero) return;

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    let animationFrame = 0;

    const update = () => {
      animationFrame = 0;
      if (reducedMotion.matches) {
        hero.style.setProperty("--hero-signal-progress", "0");
        return;
      }

      const rect = hero.getBoundingClientRect();
      const travel = Math.max(rect.height * 0.72, 1);
      const progress = clamp((82 - rect.top) / travel, 0, 1);
      hero.style.setProperty("--hero-signal-progress", progress.toFixed(4));
    };

    const requestUpdate = () => {
      if (animationFrame) return;
      animationFrame = window.requestAnimationFrame(update);
    };

    update();
    window.addEventListener("scroll", requestUpdate, { passive: true });
    window.addEventListener("resize", requestUpdate);
    reducedMotion.addEventListener("change", requestUpdate);

    return () => {
      if (animationFrame) window.cancelAnimationFrame(animationFrame);
      window.removeEventListener("scroll", requestUpdate);
      window.removeEventListener("resize", requestUpdate);
      reducedMotion.removeEventListener("change", requestUpdate);
      hero.style.removeProperty("--hero-signal-progress");
    };
  }, []);

  return (
    <div ref={rootRef} className="hero-signal" aria-hidden="true">
      <svg
        className="hero-signal__svg"
        viewBox="0 0 1000 260"
        preserveAspectRatio="none"
        focusable="false"
      >
        <defs>
          <linearGradient id="hero-signal-gradient" x1="0" y1="0" x2="1" y2="0">
            <stop offset="0" stopColor="currentColor" stopOpacity="0" />
            <stop offset="0.18" stopColor="currentColor" stopOpacity="0.25" />
            <stop offset="0.62" stopColor="currentColor" stopOpacity="0.48" />
            <stop offset="1" stopColor="currentColor" stopOpacity="0.72" />
          </linearGradient>
          <filter id="hero-signal-glow" x="-20%" y="-40%" width="140%" height="180%">
            <feGaussianBlur stdDeviation="3.2" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
          <marker
            id="hero-signal-arrow"
            markerWidth="11"
            markerHeight="11"
            refX="9"
            refY="5.5"
            orient="auto"
            markerUnits="strokeWidth"
          >
            <path d="M 0 0 L 11 5.5 L 0 11 Z" fill="currentColor" />
          </marker>
          <path
            id="hero-signal-path"
            d="M 8 220 C 94 209 146 239 224 207 S 340 128 429 160 S 542 237 638 183 S 789 76 967 55"
          />
        </defs>

        <g className="hero-signal__field" filter="url(#hero-signal-glow)">
          <use href="#hero-signal-path" className="hero-signal__path hero-signal__path--base" />
          <use
            href="#hero-signal-path"
            className="hero-signal__path hero-signal__path--trace"
            markerEnd="url(#hero-signal-arrow)"
          />
          <circle className="hero-signal__packet" r="4.5">
            <animateMotion dur="8.5s" repeatCount="indefinite" rotate="auto">
              <mpath href="#hero-signal-path" />
            </animateMotion>
          </circle>
        </g>
      </svg>
    </div>
  );
}
