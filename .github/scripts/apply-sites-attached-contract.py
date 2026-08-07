#!/usr/bin/env python3
from pathlib import Path

validator = Path('scripts/validate-design-system-integration.mjs')
s = validator.read_text()
old = '''const fittedHeaderGeometry = identityStyles.includes("grid-template-columns: 88px var(--jl-control-height-md);")
  && identityStyles.includes(".jl-site-menu {\\n  --_jl-site-menu-trigger-offset:")
  && identityStyles.includes("width: 144px;")
  && identityStyles.includes("grid-column: 1 / 3;")
  && identityStyles.includes("justify-self: end;")
  && identityStyles.includes("@media (max-width: 420px)")
  && identityStyles.includes("grid-template-columns: 88px 40px;");
if (!fittedHeaderGeometry) fail("Shared header geometry is not the approved compact-trigger/fitted-dropdown contract.");'''
new = '''const attachedHeaderGeometry = identityStyles.includes("grid-template-columns: 88px var(--jl-control-height-md);")
  && identityStyles.includes(".jl-site-menu {\\n  width: 88px;")
  && identityStyles.includes("grid-column: 1;")
  && identityStyles.includes("justify-self: stretch;")
  && identityStyles.includes("white-space: normal;")
  && identityStyles.includes("@media (max-width: 420px)")
  && identityStyles.includes("grid-template-columns: 88px 40px;")
  && !identityStyles.includes("width: 144px;")
  && !identityStyles.includes("--_jl-site-menu-trigger-offset");
if (!attachedHeaderGeometry) fail("Shared header geometry is not the approved attached-width Sites contract.");'''
if old not in s:
    raise SystemExit('Network geometry contract block not found')
validator.write_text(s.replace(old, new, 1))
