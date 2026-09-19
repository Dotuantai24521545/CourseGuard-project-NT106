## Brand & Style
The design system embodies a "Cyber-Glass" aesthetic, merging high-stakes security with futuristic transparency. It is designed for an enterprise-level cyber-security environment where clarity and protection are paramount.

The visual narrative focuses on depth, light refraction, and precision. By using glassmorphic layers, the UI suggests an interface that is both advanced and lightweight, allowing the user to "see through" complex data while remaining protected by a solid, crystalline structure. The emotional response is one of calm control within a high-tech digital frontier.

**Style Keynote:**
- **Glassmorphism:** Multi-layered translucent surfaces with high-density backdrop blurs.
- **Cyber-Glow:** Subtle, neon-tinted "light leaks" that highlight interactive zones and status indicators.
- **Precision Typography:** A blend of geometric and systematic fonts to convey authority.

## Layout & Spacing
The layout follows a strict 8px grid system to maintain mathematical precision. 

**Desktop (1440px+):** 12-column fluid grid with 24px margins. Content is organized into "Glass Modules" that span varies column widths (e.g., 4 columns for sidebars, 8 for main feeds).
**Tablet (768px - 1024px):** 8-column grid with 20px margins. Sidebars collapse into floating glass overlays.
**Mobile (<768px):** 4-column grid with 16px margins. Elements stack vertically, and glass cards utilize full-width with subtle horizontal margins.

Spacing between cards should be generous to allow the background neon glows to "breathe" through the gaps.

## Elevation & Depth
Elevation is communicated through **refraction and blur intensity** rather than traditional black shadows.

1.  **Level 0 (Floor):** The dark gradient background with occasional "Cyber Cyan" or "Hyper Purple" soft radial blurs (blur radius 200px, opacity 0.15).
2.  **Level 1 (Standard Cards):** Glass Surface (5% white), 16px Backdrop Blur, 1px Glass Border.
3.  **Level 2 (Modals/Active States):** Glass Surface (8% white), 32px Backdrop Blur, 1px Border (Secondary Color at 40% opacity). This layer uses a soft "Outer Glow" of the accent color (0px 0px 20px rgba(accent, 0.2)).
4.  **Interactive Elements:** Buttons and Toggles sit at Level 3, featuring a sharp 1px inner glow to simulate physical thickness.

## Components
- **Buttons:** 
  - *Primary:* Solid Cyber Cyan with black text for maximum contrast. Include a subtle cyan outer glow.
  - *Secondary:* Ghost glass style with a Hyper Purple border.
- **Glass Cards:** Must include `backdrop-filter: blur(12px)` and a top-to-bottom subtle white-to-transparent border gradient to simulate a light source from above.
- **Input Fields:** Semi-transparent dark fill (rgba(0,0,0,0.3)) with a glass border. On focus, the border glows Cyber Cyan.
- **Status Chips:** High-saturation pills (Cyan for "Secure", Purple for "Scanning", Red for "Threat") with a 10% background opacity of the same color.
- **Progress Indicators:** Use glowing "scanning" animations—thin cyan lines that travel across the surface of glass cards to indicate activity.
- **Data Visualizations:** Use neon stroke lines with no fills, or subtle gradient fills that mirror the background glows.