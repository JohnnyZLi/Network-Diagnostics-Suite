export const OWNED_SITES = Object.freeze([
  Object.freeze({ id: "portfolio", label: "Portfolio", href: "https://johnnyli.dev" }),
  Object.freeze({ id: "network", label: "Network Diagnostics", href: "https://network.johnnyli.dev" }),
  Object.freeze({ id: "rolepacket", label: "RolePacket", href: "https://rolepacket.johnnyli.dev" }),
]);

export const THEME_PREFERENCES = Object.freeze(["system", "light", "dark"]);

function requireElement(value, label) {
  if (!(value instanceof HTMLElement)) {
    throw new TypeError(`${label} must be an HTMLElement.`);
  }
  return value;
}

function menuItems(menu) {
  return [...menu.querySelectorAll("a[href], button:not([disabled])")];
}

function focusItem(menu, position) {
  const items = menuItems(menu);
  if (items.length === 0) return;
  const index = position === "last" ? items.length - 1 : 0;
  items[index]?.focus();
}

function themeApi(document) {
  return document.defaultView?.JLTheme ?? null;
}

function syncThemeButtons(menu) {
  const preference = themeApi(menu.ownerDocument)?.getPreference?.()
    ?? menu.ownerDocument.documentElement.dataset.themePreference
    ?? "system";
  for (const button of menu.querySelectorAll("[data-theme-preference]")) {
    if (!(button instanceof HTMLButtonElement)) continue;
    const selected = button.dataset.themePreference === preference;
    button.setAttribute("aria-pressed", String(selected));
  }
}

function createThemeControl(document) {
  const item = document.createElement("li");
  item.className = "jl-theme-menu-item";

  const label = document.createElement("span");
  label.className = "jl-theme-menu-label";
  label.textContent = "Appearance";

  const group = document.createElement("div");
  group.className = "jl-theme-options";
  group.setAttribute("role", "group");
  group.setAttribute("aria-label", "Appearance");

  for (const preference of THEME_PREFERENCES) {
    const button = document.createElement("button");
    button.type = "button";
    button.dataset.themePreference = preference;
    button.textContent = preference[0].toUpperCase() + preference.slice(1);
    button.setAttribute("aria-pressed", "false");
    group.append(button);
  }

  item.append(label, group);
  return item;
}

export function populateOwnedSites(menu, currentSite) {
  requireElement(menu, "Owned-sites menu");
  const document = menu.ownerDocument;
  const siteItems = OWNED_SITES.map((site) => {
    const item = document.createElement("li");
    const link = document.createElement("a");
    link.href = site.href;
    link.textContent = site.label;
    if (site.id === currentSite) link.setAttribute("aria-current", "page");
    item.append(link);
    return item;
  });
  menu.replaceChildren(...siteItems, createThemeControl(document));
  syncThemeButtons(menu);
}

export function installThemeControl(menu) {
  requireElement(menu, "Theme menu");
  const window = menu.ownerDocument.defaultView;

  const handleClick = (event) => {
    const button = event.target instanceof Element
      ? event.target.closest("[data-theme-preference]")
      : null;
    if (!(button instanceof HTMLButtonElement)) return;
    const preference = button.dataset.themePreference;
    if (!THEME_PREFERENCES.includes(preference)) return;
    themeApi(menu.ownerDocument)?.setPreference?.(preference);
    syncThemeButtons(menu);
  };

  const handleThemeChange = () => syncThemeButtons(menu);
  menu.addEventListener("click", handleClick);
  window?.addEventListener("jl-theme-change", handleThemeChange);
  syncThemeButtons(menu);

  return {
    destroy() {
      menu.removeEventListener("click", handleClick);
      window?.removeEventListener("jl-theme-change", handleThemeChange);
    },
  };
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
      queueMicrotask(() => focusItem(menu, focus));
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
    const items = menuItems(menu);
    if (event.key === "Escape") {
      event.preventDefault();
      close({ restoreFocus: true });
      return;
    }
    if (!items.includes(document.activeElement)) return;

    const current = items.indexOf(document.activeElement);
    let next = null;
    if (event.key === "ArrowDown") next = (current + 1) % items.length;
    if (event.key === "ArrowUp") next = (current - 1 + items.length) % items.length;
    if (event.key === "Home") next = 0;
    if (event.key === "End") next = items.length - 1;
    if (next === null) return;
    event.preventDefault();
    items[next]?.focus();
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
  const theme = installThemeControl(menu);
  const disclosure = installDisclosureMenu({
    root,
    button,
    menu,
    useHidden: true,
    onBeforeOpen: options.onBeforeOpen,
    onOpenChange: options.onOpenChange,
  });
  return {
    ...disclosure,
    destroy() {
      disclosure.destroy();
      theme.destroy();
    },
  };
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
