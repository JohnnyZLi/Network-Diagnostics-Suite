export type OwnedSiteId = "portfolio" | "network" | "rolepacket";

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

export const OWNED_SITES: readonly OwnedSite[];
export function populateOwnedSites(menu: HTMLElement, currentSite: OwnedSiteId): void;
export function installDisclosureMenu(options: DisclosureOptions): DisclosureController;
export function installSiteSwitcher(root: HTMLElement, options?: SiteSwitcherOptions): DisclosureController;
export function installHeaderMenu(root: HTMLElement, options?: Pick<SiteSwitcherOptions, "onBeforeOpen" | "onOpenChange">): DisclosureController;
