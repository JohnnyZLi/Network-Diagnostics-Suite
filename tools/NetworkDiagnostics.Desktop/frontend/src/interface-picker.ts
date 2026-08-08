const INTERFACE_SELECT = '.interface-row select[aria-label="Network interface"]';
const MENU_ID = 'network-interface-picker-menu';

type PickerState = {
  select: HTMLSelectElement;
  menu: HTMLDivElement;
  options: HTMLOptionElement[];
  activeIndex: number;
};

let picker: PickerState | null = null;

function isInterfaceSelect(target: EventTarget | null): target is HTMLSelectElement {
  return target instanceof HTMLSelectElement && target.matches(INTERFACE_SELECT);
}

function splitOptionLabel(option: HTMLOptionElement): { primary: string; secondary: string } {
  const text = (option.label || option.textContent || option.value || 'Interface').trim();
  const [primary, ...rest] = text.split('·').map((part) => part.trim()).filter(Boolean);
  return {
    primary: primary || text,
    secondary: rest.join(' · ') || (option.value ? '' : 'System default'),
  };
}

function closePicker(returnFocus = false) {
  if (!picker) return;
  const { select, menu } = picker;
  picker = null;
  menu.remove();
  select.classList.remove('app-select-open');
  select.removeAttribute('aria-expanded');
  select.removeAttribute('aria-controls');
  if (returnFocus && document.contains(select)) select.focus({ preventScroll: true });
}

function updateActiveOption() {
  if (!picker) return;
  const optionButtons = Array.from(picker.menu.querySelectorAll<HTMLButtonElement>('.app-select-option'));
  optionButtons.forEach((button, index) => button.classList.toggle('active', index === picker?.activeIndex));
  optionButtons[picker.activeIndex]?.scrollIntoView({ block: 'nearest' });
}

function moveActiveOption(delta: number) {
  if (!picker || !picker.options.length) return;
  const count = picker.options.length;
  picker.activeIndex = (picker.activeIndex + delta + count) % count;
  updateActiveOption();
}

function setActiveOption(index: number) {
  if (!picker || !picker.options.length) return;
  picker.activeIndex = Math.max(0, Math.min(index, picker.options.length - 1));
  updateActiveOption();
}

function chooseActiveOption() {
  if (!picker) return;
  const option = picker.options[picker.activeIndex];
  if (!option) return;
  chooseOption(option);
}

function chooseOption(option: HTMLOptionElement) {
  if (!picker) return;
  const select = picker.select;
  if (select.value !== option.value) {
    select.value = option.value;
    select.dispatchEvent(new Event('change', { bubbles: true }));
  }
  closePicker(true);
}

function positionPicker() {
  if (!picker) return;
  const { select, menu } = picker;
  const rect = select.getBoundingClientRect();
  const viewportWidth = window.innerWidth;
  const viewportHeight = window.innerHeight;
  const margin = 8;
  const gap = 6;
  const width = Math.min(Math.max(rect.width, 300), Math.max(240, viewportWidth - margin * 2));

  menu.style.width = `${width}px`;
  const left = Math.min(Math.max(margin, rect.left), Math.max(margin, viewportWidth - width - margin));
  menu.style.left = `${left}px`;

  const below = Math.max(0, viewportHeight - rect.bottom - margin - gap);
  const above = Math.max(0, rect.top - margin - gap);
  const naturalHeight = menu.scrollHeight;
  const prefersAbove = below < Math.min(240, naturalHeight) && above > below;
  const available = Math.max(120, prefersAbove ? above : below);
  const maxHeight = Math.min(320, available);
  menu.style.maxHeight = `${maxHeight}px`;

  const renderedHeight = Math.min(naturalHeight, maxHeight);
  const top = prefersAbove
    ? Math.max(margin, rect.top - renderedHeight - gap)
    : Math.min(viewportHeight - renderedHeight - margin, rect.bottom + gap);
  menu.style.top = `${Math.max(margin, top)}px`;
  menu.classList.toggle('open-above', prefersAbove);
}

function openPicker(select: HTMLSelectElement, initialMove = 0) {
  if (select.disabled) return;
  if (picker?.select === select) {
    closePicker(true);
    return;
  }
  closePicker();

  const options = Array.from(select.options).filter((option) => !option.disabled);
  if (!options.length) return;

  const selectedIndex = Math.max(0, options.findIndex((option) => option.selected || option.value === select.value));
  const menu = document.createElement('div');
  menu.id = MENU_ID;
  menu.className = 'app-select-menu';
  menu.setAttribute('role', 'listbox');
  menu.setAttribute('aria-label', select.getAttribute('aria-label') || 'Select option');

  options.forEach((option, index) => {
    const parts = splitOptionLabel(option);
    const row = document.createElement('button');
    row.type = 'button';
    row.className = `app-select-option${option.value === '' ? ' automatic' : ''}${option.selected || option.value === select.value ? ' selected' : ''}`;
    row.setAttribute('role', 'option');
    row.setAttribute('aria-selected', option.selected || option.value === select.value ? 'true' : 'false');
    row.tabIndex = -1;

    const check = document.createElement('span');
    check.className = 'app-select-check';
    check.setAttribute('aria-hidden', 'true');
    check.textContent = option.selected || option.value === select.value ? '✓' : '';

    const primary = document.createElement('span');
    primary.className = 'app-select-primary';
    primary.textContent = parts.primary;

    const secondary = document.createElement('span');
    secondary.className = 'app-select-secondary';
    secondary.textContent = parts.secondary;

    row.append(check, primary, secondary);
    row.addEventListener('pointerenter', () => {
      if (!picker) return;
      picker.activeIndex = index;
      updateActiveOption();
    });
    row.addEventListener('pointerdown', (event) => event.preventDefault());
    row.addEventListener('click', () => chooseOption(option));
    menu.appendChild(row);
  });

  document.body.appendChild(menu);
  picker = { select, menu, options, activeIndex: selectedIndex };
  select.classList.add('app-select-open');
  select.setAttribute('aria-expanded', 'true');
  select.setAttribute('aria-controls', MENU_ID);
  select.focus({ preventScroll: true });
  positionPicker();

  if (initialMove) moveActiveOption(initialMove);
  else updateActiveOption();
}

document.addEventListener('pointerdown', (event) => {
  const target = event.target;
  if (isInterfaceSelect(target)) {
    if (target.disabled) return;
    event.preventDefault();
    openPicker(target);
    return;
  }
  if (picker && target instanceof Node && !picker.menu.contains(target)) closePicker();
}, true);

// A prevented pointer-down suppresses the native popup in WebKit, but the follow-up
// click is also cancelled so macOS cannot reopen its system menu on the same gesture.
document.addEventListener('click', (event) => {
  if (!isInterfaceSelect(event.target)) return;
  event.preventDefault();
}, true);

document.addEventListener('keydown', (event) => {
  const focused = document.activeElement;
  if (!isInterfaceSelect(focused) || focused.disabled) {
    if (picker && event.key === 'Escape') {
      event.preventDefault();
      closePicker(true);
    }
    return;
  }

  if (!picker) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      openPicker(focused, event.key === 'ArrowDown' ? 1 : event.key === 'ArrowUp' ? -1 : 0);
    }
    return;
  }

  switch (event.key) {
    case 'ArrowDown':
      event.preventDefault();
      moveActiveOption(1);
      break;
    case 'ArrowUp':
      event.preventDefault();
      moveActiveOption(-1);
      break;
    case 'Home':
      event.preventDefault();
      setActiveOption(0);
      break;
    case 'End':
      event.preventDefault();
      setActiveOption(picker.options.length - 1);
      break;
    case 'Enter':
    case ' ':
      event.preventDefault();
      chooseActiveOption();
      break;
    case 'Escape':
      event.preventDefault();
      closePicker(true);
      break;
    case 'Tab':
      closePicker();
      break;
  }
});

document.addEventListener('change', (event) => {
  if (picker && event.target === picker.select) closePicker();
});

document.addEventListener('scroll', (event) => {
  if (!picker) return;
  const target = event.target;
  if (target instanceof Node && picker.menu.contains(target)) return;
  closePicker();
}, true);

window.addEventListener('resize', () => closePicker());
