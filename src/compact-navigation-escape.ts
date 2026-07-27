export function installCompactNavigationEscape(): void {
  document.addEventListener(
    "keydown",
    (event) => {
      if (event.key !== "Escape") return;
      const button = document.querySelector('.nav-toggle[aria-expanded="true"]');
      if (!(button instanceof HTMLButtonElement)) return;
      const controlledId = button.getAttribute("aria-controls");
      const navigation = controlledId ? document.getElementById(controlledId) : null;
      if (!(navigation instanceof HTMLElement)) return;

      event.preventDefault();
      event.stopImmediatePropagation();
      button.click();
      button.focus();
    },
    { capture: true },
  );
}
