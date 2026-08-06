# Shared web appearance

The browser client consumes Web Design System 1.9.0 from commit `aca8bf9f4c5c2b93a123ac91ca804b4079ec64b9`.

System, Light, and Dark resolve before React and product styles load. Appearance remains inside the shared Sites menu, follows operating-system changes while set to System, and persists across owned production domains.

Release validation renders both modes at desktop, mobile, and 320-pixel widths and checks contrast, focus, persistence, menu state, analytical preview rendering, and horizontal containment.

This preference changes the browser product only. Native desktop appearance remains owned by the desktop application and its separate release process.
