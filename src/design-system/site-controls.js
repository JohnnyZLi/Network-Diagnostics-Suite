export const OWNED_SITES = Object.freeze([
  Object.freeze({ id: "portfolio", label: "Portfolio", href: "https://johnnyli.dev" }),
  Object.freeze({ id: "network", label: "Network Diagnostics", href: "https://network.johnnyli.dev" }),
  Object.freeze({ id: "rolepacket", label: "RolePacket", href: "https://rolepacket.johnnyli.dev" }),
]);

function requireElement(value, label) {
  if (!(value instanceof HTMLElement)) {
    throw new TypeError(`${label} must be an HTMLElement.`);
  }
  return value;
}

function menuLinks(menu) {
  return [...menu.querySelectorAll("a[href]")];
}

function focusLink(menu, position) {
  const links = menuLinks(menu);
  if (links.length === 0) return;
  const index = position === "last" ? links.length - 1 : 0;
  links[index]?.focus();
}

export function populateOwnedSites(menu, currentSite) {
  requireElement(menu, "Owned-sites menu");
  const document = menu.ownerDocument;
  menu.replaceChildren(...OWNED_SITES.map((site) => {
    const item = document.createElement("li");
    const link = document.createElement("a");
    link.href = site.href;
    link.textContent = site.label;
    if (site.id === currentSite) link.setAttribute("aria-current", "page");
    item.append(link);
    return item;
  }));
}

export function installDisclosureMenu({
  root,
  button,
  menu,
  openClass = "",
  useHidden = false,
  closeOnSelect = true,
  closeMediaQuery = null,
  onBeforeOpen = null,
  onOpenChange = null,
}) {
  requireElement(root, "Disclosure root");
  if (!(button instanceof HTMLButtonElement)) {
    throw new TypeError("Disclosure button must be an HTMLButtonElement.");
  }
  requireElement(menu, "Disclosure menu");

  const document = root.ownerDocument;
  const window = document.defaultView;
  let open = button.getAttribute("aria-expanded") === "true";

  const applyState = (nextOpen, { restoreFocus = false, focus = null } = {}) => {
    if (nextOpen && !open) onBeforeOpen?.();
    open = nextOpen;
    button.setAttribute("aria-expanded", String(open));
    if (useHidden) menu.hidden = !open;
    if (openClass) menu.classList.toggle(openClass, open);
    onOpenChange?.(open);

    if (open && focus) {
      queueMicrotask(() => focusLink(menu, focus));
    } else if (!open && restoreFocus) {
      button.focus();
    }
  };

  const close = (options = {}) => applyState(false, options);
  const openMenu = (options = {}) => applyState(true, options);
  const toggle = () => applyState(!open);

  const handleButtonClick = () => toggle();
  const handleButtonKeyDown = (event) => {
    if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
    event.preventDefault();
    openMenu({ focus: event.key === "ArrowUp" ? "last" : "first" });
  };
  const handleMenuKeyDown = (event) => {
    const links = menuLinks(menu);
    if (event.key === "Escape") {
      event.preventDefault();
      close({ restoreFocus: true });
      return;
    }
    if (!links.includes(document.activeElement)) return;

    const current = links.indexOf(document.activeElement);
    let next = null;
    if (event.key === "ArrowDown") next = (current + 1) % links.length;
    if (event.key === "ArrowUp") next = (current - 1 + links.length) % links.length;
    if (event.key === "Home") next = 0;
    if (event.key === "End") next = links.length - 1;
    if (next === null) return;
    event.preventDefault();
    links[next]?.focus();
  };
  const handleMenuClick = (event) => {
    if (closeOnSelect && event.target instanceof Element && event.target.closest("a[href]")) close();
  };
  const handlePointerDown = (event) => {
    if (open && event.target instanceof Node && !root.contains(event.target)) close();
  };
  const handleDocumentKeyDown = (event) => {
    if (open && event.key === "Escape") {
      event.preventDefault();
      close({ restoreFocus: true });
    }
  };

  const media = closeMediaQuery && window ? window.matchMedia(closeMediaQuery) : null;
  const handleMediaChange = (event) => {
    if (event.matches) close();
  };

  button.addEventListener("click", handleButtonClick);
  button.addEventListener("keydown", handleButtonKeyDown);
  menu.addEventListener("keydown", handleMenuKeyDown);
  menu.addEventListener("click", handleMenuClick);
  document.addEventListener("pointerdown", handlePointerDown);
  document.addEventListener("keydown", handleDocumentKeyDown);
  media?.addEventListener("change", handleMediaChange);
  applyState(open);

  return {
    close,
    open: openMenu,
    toggle,
    destroy() {
      button.removeEventListener("click", handleButtonClick);
      button.removeEventListener("keydown", handleButtonKeyDown);
      menu.removeEventListener("keydown", handleMenuKeyDown);
      menu.removeEventListener("click", handleMenuClick);
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleDocumentKeyDown);
      media?.removeEventListener("change", handleMediaChange);
    },
  };
}

export function installSiteSwitcher(root, options = {}) {
  const button = root.querySelector("[data-site-switcher-button]");
  const menu = root.querySelector("[data-site-switcher-menu]");
  if (options.populate && options.currentSite) populateOwnedSites(menu, options.currentSite);
  return installDisclosureMenu({
    root,
    button,
    menu,
    useHidden: true,
    onBeforeOpen: options.onBeforeOpen,
    onOpenChange: options.onOpenChange,
  });
}

export function installHeaderMenu(root, options = {}) {
  const button = root.querySelector("[data-header-menu-button]");
  const menu = root.querySelector("[data-header-menu]");
  return installDisclosureMenu({
    root,
    button,
    menu,
    openClass: "jl-header-menu--open",
    closeMediaQuery: "(min-width: 901px)",
    onBeforeOpen: options.onBeforeOpen,
    onOpenChange: options.onOpenChange,
  });
}
