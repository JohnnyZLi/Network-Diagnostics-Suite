#!/usr/bin/env python3
from pathlib import Path

integration = Path('scripts/validate-design-system-integration.mjs')
s = integration.read_text()
s = s.replace('const attachedHeaderGeometry = identityStyles.includes("grid-template-columns: 88px var(--jl-control-height-md);")', 'const attachedHeaderGeometry = identityStyles.includes("grid-template-columns: 104px var(--jl-control-height-md);")')
s = s.replace('&& identityStyles.includes(".jl-site-menu {\\n  width: 88px;")', '&& identityStyles.includes(".jl-site-menu {\\n  width: 104px;")')
s = s.replace('&& identityStyles.includes("grid-template-columns: 88px 40px;")', '&& identityStyles.includes("grid-template-columns: 104px 40px;")\n  && identityStyles.includes("grid-template-columns: 96px 40px;")')
integration.write_text(s)

visual = Path('.github/workflows/visual-audit.yml')
s = visual.read_text()
s = s.replace('const fittedSitesWidth = 88;', 'const fittedSitesWidth = metrics.viewportWidth <= 360 ? 96 : 104;')
s = s.replace('if (!near(sitesOpenGeometry.buttonWidth, 88)) problems.push(`Sites trigger width ${sitesOpenGeometry.buttonWidth}`);\n              if (!near(sitesOpenGeometry.menuWidth, 88)) problems.push(`Sites dropdown width ${sitesOpenGeometry.menuWidth}`);', 'const expectedOpenSitesWidth = sitesOpenGeometry.viewportWidth <= 360 ? 96 : 104;\n              if (!near(sitesOpenGeometry.buttonWidth, expectedOpenSitesWidth)) problems.push(`Sites trigger width ${sitesOpenGeometry.buttonWidth}, expected ${expectedOpenSitesWidth}`);\n              if (!near(sitesOpenGeometry.menuWidth, expectedOpenSitesWidth)) problems.push(`Sites dropdown width ${sitesOpenGeometry.menuWidth}, expected ${expectedOpenSitesWidth}`);')
visual.write_text(s)
