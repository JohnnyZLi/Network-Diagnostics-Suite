import { useLayoutEffect } from "react";

const MOTION_TARGETS = [
  ".measurement-preview > .section-heading",
  ".preview-grid article",
  ".methodology > .section-heading",
  ".methodology-grid article",
  ".deep-probe__intro",
  ".deep-probe__actions",
  ".information-panel",
  ".results > .section-heading",
  ".metric-card",
  ".report-panel",
  ".deep-report > .section-heading",
  ".deep-summary",
  ".deep-table-wrap",
  ".interface-grid"
].join(",");

export function MotionObserver() {
  useLayoutEffect(() => {
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    if (reducedMotion.matches || !("IntersectionObserver" in window)) return;

    const root = document.documentElement;
    root.classList.add("motion-ready");

    const intersectionObserver = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add("is-visible");
        intersectionObserver.unobserve(entry.target);
      });
    }, { threshold: 0.12, rootMargin: "0px 0px -8% 0px" });

    const registerTargets = (scope: ParentNode) => {
      scope.querySelectorAll<HTMLElement>(MOTION_TARGETS).forEach((target, index) => {
        if (target.dataset.motionBound === "true") return;
        target.dataset.motionBound = "true";
        target.classList.add("motion-reveal");
        target.style.setProperty("--motion-delay", `${(index % 4) * 55}ms`);
        intersectionObserver.observe(target);
      });
    };

    registerTargets(document);

    const mutationObserver = new MutationObserver((mutations) => {
      mutations.forEach((mutation) => {
        mutation.addedNodes.forEach((node) => {
          if (node instanceof Element) registerTargets(node.parentNode ?? document);
        });
      });
    });

    mutationObserver.observe(document.getElementById("root") ?? document.body, {
      childList: true,
      subtree: true
    });

    return () => {
      mutationObserver.disconnect();
      intersectionObserver.disconnect();
      root.classList.remove("motion-ready");
      document.querySelectorAll<HTMLElement>("[data-motion-bound='true']").forEach((target) => {
        delete target.dataset.motionBound;
        target.classList.remove("motion-reveal", "is-visible");
        target.style.removeProperty("--motion-delay");
      });
    };
  }, []);

  return null;
}
