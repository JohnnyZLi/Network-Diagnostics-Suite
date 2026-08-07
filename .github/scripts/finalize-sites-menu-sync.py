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

workflow = Path('.github/workflows/visual-audit.yml')
s = workflow.read_text()
s = s.replace("const fittedSitesWidth = metrics.viewportWidth <= 420 ? 116 : 136;", "const fittedSitesWidth = 88;")
s = s.replace(
'''      return {
        buttonWidth: button?.getBoundingClientRect().width ?? null,
        menuWidth: menu?.getBoundingClientRect().width ?? null,
        linkOverflow: links.some((link) => link.scrollWidth > link.clientWidth + 1),
        linkWrapping: links.some((link) => getComputedStyle(link).whiteSpace !== 'nowrap'),
      };''',
'''      const menuRect = menu?.getBoundingClientRect();
      return {
        buttonWidth: button?.getBoundingClientRect().width ?? null,
        menuWidth: menuRect?.width ?? null,
        menuLeft: menuRect?.left ?? null,
        menuRight: menuRect?.right ?? null,
        viewportWidth: window.innerWidth,
        linkOverflow: links.some((link) => link.scrollWidth > link.clientWidth + 1),
        linkWrapping: links.some((link) => getComputedStyle(link).whiteSpace !== 'nowrap'),
      };''')
s = s.replace(
"if (!near(sitesOpenGeometry.buttonWidth, sitesOpenGeometry.menuWidth)) problems.push('Sites extension does not match trigger width');",
"if (!near(sitesOpenGeometry.buttonWidth, 88)) problems.push(`Sites trigger width ${sitesOpenGeometry.buttonWidth}`);\n    if (!near(sitesOpenGeometry.menuWidth, 144)) problems.push(`Sites dropdown width ${sitesOpenGeometry.menuWidth}`);\n    if (sitesOpenGeometry.menuLeft < -0.5 || sitesOpenGeometry.menuRight > sitesOpenGeometry.viewportWidth + 0.5) problems.push('Sites dropdown escapes the viewport');")
workflow.write_text(s)
