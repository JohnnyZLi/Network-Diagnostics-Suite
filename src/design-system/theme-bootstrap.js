(() => {
  "use strict";

  // The bootstrap owns the root data-theme and data-theme-preference attributes.
  const GLOBAL_KEY = "JLTheme";
  if (window[GLOBAL_KEY]) return;

  const root = document.documentElement;
  const storageKey = "jl-theme";
  const cookieName = "jl-theme";
  const preferences = Object.freeze(["system", "light", "dark"]);
  const systemQuery = window.matchMedia("(prefers-color-scheme: dark)");
  let printTheme = null;

  const isPreference = (value) => preferences.includes(value);

  const readCookie = () => {
    const prefix = `${cookieName}=`;
    for (const part of document.cookie.split(";")) {
      const value = part.trim();
      if (value.startsWith(prefix)) return decodeURIComponent(value.slice(prefix.length));
    }
    return null;
  };

  const readStoredPreference = () => {
    const cookie = readCookie();
    if (isPreference(cookie)) return cookie;
    try {
      const stored = window.localStorage.getItem(storageKey);
      if (isPreference(stored)) return stored;
    } catch {
      // Storage can be unavailable in hardened or private contexts.
    }
    return "system";
  };

  const resolveTheme = (preference) => (
    preference === "system"
      ? (systemQuery.matches ? "dark" : "light")
      : preference
  );

  const syncThemeColor = (theme) => {
    const meta = document.querySelector('meta[name="theme-color"]');
    if (!(meta instanceof HTMLMetaElement)) return;
    const value = theme === "dark" ? meta.dataset.themeDark : meta.dataset.themeLight;
    if (value) meta.content = value;
  };

  const writePreference = (preference) => {
    try {
      window.localStorage.setItem(storageKey, preference);
    } catch {
      // The cookie remains the cross-subdomain persistence mechanism.
    }

    const domain = location.hostname === "johnnyli.dev" || location.hostname.endsWith(".johnnyli.dev")
      ? "; Domain=.johnnyli.dev"
      : "";
    document.cookie = `${cookieName}=${encodeURIComponent(preference)}; Max-Age=31536000; Path=/; SameSite=Lax${domain}`;
  };

  const applyPreference = (preference, { persist = false, announce = true } = {}) => {
    const nextPreference = isPreference(preference) ? preference : "system";
    const theme = resolveTheme(nextPreference);
    root.dataset.themePreference = nextPreference;
    root.dataset.theme = theme;
    syncThemeColor(theme);
    if (persist) writePreference(nextPreference);
    if (announce) {
      window.dispatchEvent(new CustomEvent("jl-theme-change", {
        detail: Object.freeze({ preference: nextPreference, theme }),
      }));
    }
    return theme;
  };

  const api = Object.freeze({
    preferences,
    getPreference: () => root.dataset.themePreference || "system",
    getTheme: () => root.dataset.theme || resolveTheme("system"),
    setPreference: (preference) => applyPreference(preference, { persist: true }),
    applyPreference,
  });

  Object.defineProperty(window, GLOBAL_KEY, {
    configurable: false,
    enumerable: false,
    writable: false,
    value: api,
  });

  applyPreference(readStoredPreference(), { announce: false });

  systemQuery.addEventListener("change", () => {
    if (api.getPreference() === "system") applyPreference("system");
  });

  window.addEventListener("storage", (event) => {
    if (event.key === storageKey && isPreference(event.newValue)) {
      applyPreference(event.newValue, { announce: true });
    }
  });

  window.addEventListener("beforeprint", () => {
    if (!printTheme) {
      printTheme = { preference: api.getPreference(), theme: api.getTheme() };
    }
    root.dataset.theme = "light";
    syncThemeColor("light");
  });

  window.addEventListener("afterprint", () => {
    if (!printTheme) return;
    root.dataset.theme = printTheme.theme;
    syncThemeColor(printTheme.theme);
    printTheme = null;
  });
})();
