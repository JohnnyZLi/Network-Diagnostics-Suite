#!/usr/bin/env python3
from pathlib import Path

validator = Path('scripts/validate-design-system-integration.mjs')
s = validator.read_text()
start = s.index('const legacyHeaderGeometry =')
end = s.index('\n\nrequireFragments(updater', start)
s = s[:start] + '''const fittedHeaderGeometry = identityStyles.includes("grid-template-columns: 88px var(--jl-control-height-md);")
  && identityStyles.includes(".jl-site-menu {\\n  --_jl-site-menu-trigger-offset:")
  && identityStyles.includes("width: 144px;")
  && identityStyles.includes("grid-column: 1 / 3;")
  && identityStyles.includes("justify-self: end;")
  && identityStyles.includes("@media (max-width: 420px)")
  && identityStyles.includes("grid-template-columns: 88px 40px;");
if (!fittedHeaderGeometry) fail("Shared header geometry is not the approved compact-trigger/fitted-dropdown contract.");''' + s[end:]
validator.write_text(s)
