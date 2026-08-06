export type OwnedSiteId = "portfolio" | "network" | "rolepacket";
export type ThemePreference = "system" | "light" | "dark";
export type ResolvedTheme = "light" | "dark";

export interface OwnedSite {
  readonly id: OwnedSiteId;
  readonly label: string;
  readonly href: string;
}

export interface DisclosureController {
  close(options?: { restoreFocus?: boolean }): void;
  open(options?: { focus?: "first" | "last" }): void;
  toggle(): void;
  destroy(): void;
}

export interface ThemeControlController {
  destroy(): void;
}

export interface DisclosureOptions {
  root: HTMLElement;
  button: HTMLButtonElement;
  menu: HTMLElement;
  openClass?: string;
  useHidden?: boolean;
  closeOnSelect?: boolean;
  closeMediaQuery?: string | null;
  onBeforeOpen?: (() => void) | null;
  onOpenChange?: ((open: boolean) => void) | null;
}

export interface SiteSwitcherOptions {
  currentSite?: OwnedSiteId;
  populate?: boolean;
  onBeforeOpen?: (() => void) | null;
  onOpenChange?: ((open: boolean) => void) | null;
}

declare global {
  interface Window {
    JLTheme?: Readonly<{
      preferences: readonly ThemePreference[];
      getPreference(): ThemePreference;
      getTheme(): ResolvedTheme;
      setPreference(preference: ThemePreference): ResolvedTheme;
      applyPreference(preference: ThemePreference, options?: { persist?: boolean; announce?: boolean }): ResolvedTheme;
    }>;
  }
}

export const OWNED_SITES: readonly OwnedSite[];
export const THEME_PREFERENCES: readonly ThemePreference[];
export function populateOwnedSites(menu: HTMLElement, currentSite: OwnedSiteId): void;
export function installThemeControl(menu: HTMLElement): ThemeControlController;
export function installDisclosureMenu(options: DisclosureOptions): DisclosureController;
export function installSiteSwitcher(root: HTMLElement, options?: SiteSwitcherOptions): DisclosureController;
export function installHeaderMenu(root: HTMLElement, options?: Pick<SiteSwitcherOptions, "onBeforeOpen" | "onOpenChange">): DisclosureController;
