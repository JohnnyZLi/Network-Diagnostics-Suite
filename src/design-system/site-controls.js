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

function createThemeIcon(document, preference) {
  const namespace = "http" + "://www.w3.org/2000/svg";
  const svg = document.createElementNS(namespace, "svg");
  svg.classList.add("jl-theme-option__icon");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("aria-hidden", "true");
  svg.setAttribute("fill", "none");
  svg.setAttribute("stroke", "currentColor");
  svg.setAttribute("stroke-width", "1.8");
  svg.setAttribute("stroke-linecap", "round");
  svg.setAttribute("stroke-linejoin", "round");

  if (preference === "system") {
    const screen = document.createElementNS(namespace, "rect");
    screen.setAttribute("x", "3");
    screen.setAttribute("y", "4");
    screen.setAttribute("width", "18");
    screen.setAttribute("height", "12");
    screen.setAttribute("rx", "2");
    const stand = document.createElementNS(namespace, "path");
    stand.setAttribute("d", "M8 20h8M12 16v4");
    svg.append(screen, stand);
    return svg;
  }

  if (preference === "light") {
    const sun = document.createElementNS(namespace, "circle");
    sun.setAttribute("cx", "12");
    sun.setAttribute("cy", "12");
    sun.setAttribute("r", "4");
    const rays = document.createElementNS(namespace, "path");
    rays.setAttribute("d", "M12 2v2M12 20v2M4.93 4.93l1.42 1.42M17.65 17.65l1.42 1.42M2 12h2M20 12h2M4.93 19.07l1.42-1.42M17.65 6.35l1.42-1.42");
    svg.append(sun, rays);
    return svg;
  }

  const moon = document.createElementNS(namespace, "path");
  moon.setAttribute("d", "M20.5 14.2A8.5 8.5 0 0 1 9.8 3.5 8.5 8.5 0 1 0 20.5 14.2Z");
  svg.append(moon);
  return svg;
}

function createThemeControl(document) {
  const group = document.createElement("div");
  group.className = "jl-theme-options";
  group.setAttribute("role", "group");
  group.setAttribute("aria-label", "Appearance");

  for (const preference of THEME_PREFERENCES) {
    const button = document.createElement("button");
    const label = preference[0].toUpperCase() + preference.slice(1);
    button.type = "button";
    button.dataset.themePreference = preference;
    button.setAttribute("aria-pressed", "false");
    button.setAttribute("aria-label", label);
    button.title = label;
    button.append(createThemeIcon(document, preference));
    group.append(button);
  }

  return group;
}

function createSettingsIcon(document) {
  const namespace = "http" + "://www.w3.org/2000/svg";
  const svg = document.createElementNS(namespace, "svg");
  svg.classList.add("jl-settings-button__icon");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("aria-hidden", "true");
  svg.setAttribute("fill", "none");
  svg.setAttribute("stroke", "currentColor");
  svg.setAttribute("stroke-width", "1.8");
  svg.setAttribute("stroke-linecap", "round");
  svg.setAttribute("stroke-linejoin", "round");

  const gear = document.createElementNS(namespace, "path");
  gear.setAttribute("d", "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.09a2 2 0 0 1 1 1.74v.5a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.38a2 2 0 0 0-.73-2.73l-.15-.09a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z");
  const hub = document.createElementNS(namespace, "circle");
  hub.setAttribute("cx", "12");
  hub.setAttribute("cy", "12");
  hub.setAttribute("r", "3");
  svg.append(gear, hub);
  return svg;
}

function ensureSettingsControl(root, sitesButton, sitesMenu) {
  const document = root.ownerDocument;
  let button = root.querySelector("[data-settings-button]");
  let menu = root.querySelector("[data-settings-menu]");

  if (!(menu instanceof HTMLElement)) {
    menu = document.createElement("div");
    menu.className = "jl-settings-menu";
    menu.dataset.settingsMenu = "";
    menu.id = sitesMenu.id ? `${sitesMenu.id}-settings` : "jl-settings-menu";
    menu.setAttribute("aria-label", "Settings");
    menu.hidden = true;
    menu.append(createThemeControl(document));
    root.append(menu);
  }

  if (!(button instanceof HTMLButtonElement)) {
    button = document.createElement("button");
    button.type = "button";
    button.className = "jl-settings-button";
    button.dataset.settingsButton = "";
    button.setAttribute("aria-label", "Settings");
    button.setAttribute("aria-expanded", "false");
    button.setAttribute("aria-controls", menu.id);
    button.append(createSettingsIcon(document));
    sitesButton.after(button);
  }

  return { button, menu };
}

function ensureDisclosureShell(root, button, menu, className) {
  let shell = button.parentElement;
  if (!(shell instanceof HTMLElement) || !shell.classList.contains(className)) {
    shell = root.ownerDocument.createElement("div");
    shell.className = className;
    root.insertBefore(shell, button);
  }
  if (button.parentElement !== shell) shell.append(button);
  if (menu.parentElement !== shell) shell.append(menu);
  return shell;
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
  menu.replaceChildren(...siteItems);
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

  const handleButtonClick = (event) => {
    toggle();
    if (event.detail > 0) button.blur();
  };
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

function installHeaderDisclosureExit(root) {
  const header = root.closest(".jl-global-header, .jl-site-header");
  const inner = header?.querySelector(".jl-global-header__inner");
  const window = root.ownerDocument.defaultView;
  let timer = null;
  let animationEndHandler = null;
  let sequence = 0;

  const hasOpenDisclosure = () => root.querySelector(
    '[data-site-switcher-button][aria-expanded="true"], [data-settings-button][aria-expanded="true"]',
  ) !== null;

  const clearExit = () => {
    if (timer !== null && window) window.clearTimeout(timer);
    timer = null;
    if (animationEndHandler && inner instanceof HTMLElement) inner.removeEventListener("animationend", animationEndHandler);
    animationEndHandler = null;
    if (header instanceof HTMLElement) {
      header.removeAttribute("data-jl-header-disclosure-exit");
      header.style.removeProperty("--_jl-header-disclosure-exit-y");
    }
  };

  const cancel = () => { sequence += 1; clearExit(); };

  const beginExit = () => {
    if (!(header instanceof HTMLElement) || !(inner instanceof HTMLElement) || !window || hasOpenDisclosure()) { clearExit(); return; }
    const naturalRect = header.getBoundingClientRect();
    const fixedRect = inner.getBoundingClientRect();
    const distance = Math.min(fixedRect.height, Math.max(0, -naturalRect.top));
    if (distance < 1) { clearExit(); return; }
    header.style.setProperty("--_jl-header-disclosure-exit-y", `${-distance}px`);
    header.setAttribute("data-jl-header-disclosure-exit", "");
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) { queueMicrotask(clearExit); return; }
    const currentSequence = ++sequence;
    animationEndHandler = (event) => {
      if (event.animationName !== "jl-header-disclosure-exit" || currentSequence !== sequence) return;
      clearExit();
    };
    inner.addEventListener("animationend", animationEndHandler);
    timer = window.setTimeout(() => { if (currentSequence === sequence) clearExit(); }, 260);
  };

  const sync = (open) => {
    if (open) { cancel(); return; }
    const currentSequence = ++sequence;
    queueMicrotask(() => {
      if (currentSequence !== sequence) return;
      if (hasOpenDisclosure()) { clearExit(); return; }
      beginExit();
    });
  };

  return { cancel, sync, destroy: cancel };
}

export function installSiteSwitcher(root, options = {}) {
  const button = root.querySelector("[data-site-switcher-button]");
  const menu = root.querySelector("[data-site-switcher-menu]");
  if (!(button instanceof HTMLButtonElement)) {
    throw new TypeError("Site switcher button must be an HTMLButtonElement.");
  }
  requireElement(menu, "Site switcher menu");
  if (options.populate && options.currentSite) populateOwnedSites(menu, options.currentSite);

  const settings = ensureSettingsControl(root, button, menu);
  ensureDisclosureShell(root, button, menu, "jl-site-disclosure");
  ensureDisclosureShell(root, settings.button, settings.menu, "jl-settings-disclosure");
  const theme = installThemeControl(settings.menu);
  const headerExit = installHeaderDisclosureExit(root);
  let settingsDisclosure = null;

  const sitesDisclosure = installDisclosureMenu({
    root,
    button,
    menu,
    useHidden: true,
    onBeforeOpen: () => {
      headerExit.cancel();
      settingsDisclosure?.close();
      options.onBeforeOpen?.();
    },
    onOpenChange: (open) => {
      headerExit.sync(open);
      options.onOpenChange?.(open);
    },
  });

  settingsDisclosure = installDisclosureMenu({
    root,
    button: settings.button,
    menu: settings.menu,
    useHidden: true,
    closeOnSelect: false,
    onBeforeOpen: () => {
      headerExit.cancel();
      sitesDisclosure.close();
      options.onBeforeOpen?.();
    },
    onOpenChange: headerExit.sync,
  });

  return {
    open: sitesDisclosure.open,
    toggle: sitesDisclosure.toggle,
    close(options = {}) {
      sitesDisclosure.close(options);
      settingsDisclosure.close();
    },
    destroy() {
      sitesDisclosure.destroy();
      settingsDisclosure.destroy();
      theme.destroy();
      headerExit.destroy();
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
