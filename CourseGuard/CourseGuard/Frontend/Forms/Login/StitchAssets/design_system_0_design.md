---
name: Cyber-Glass Intelligence
colors:
  surface: '#13131b'
  surface-dim: '#13131b'
  surface-bright: '#393842'
  surface-container-lowest: '#0d0d16'
  surface-container-low: '#1b1b23'
  surface-container: '#1f1f27'
  surface-container-high: '#292932'
  surface-container-highest: '#34343d'
  on-surface: '#e4e1ed'
  on-surface-variant: '#b9cacb'
  inverse-surface: '#e4e1ed'
  inverse-on-surface: '#302f39'
  outline: '#849495'
  outline-variant: '#3b494b'
  surface-tint: '#00dbe9'
  primary: '#dbfcff'
  on-primary: '#00363a'
  primary-container: '#00f0ff'
  on-primary-container: '#006970'
  inverse-primary: '#006970'
  secondary: '#dfb7ff'
  on-secondary: '#4b007e'
  secondary-container: '#9606f4'
  on-secondary-container: '#f2dcff'
  tertiary: '#f9f3ff'
  on-tertiary: '#322d43'
  tertiary-container: '#ddd5f2'
  on-tertiary-container: '#615b73'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#7df4ff'
  primary-fixed-dim: '#00dbe9'
  on-primary-fixed: '#002022'
  on-primary-fixed-variant: '#004f54'
  secondary-fixed: '#f1daff'
  secondary-fixed-dim: '#dfb7ff'
  on-secondary-fixed: '#2d004f'
  on-secondary-fixed-variant: '#6b00b0'
  tertiary-fixed: '#e7defb'
  tertiary-fixed-dim: '#cac3de'
  on-tertiary-fixed: '#1d192d'
  on-tertiary-fixed-variant: '#49445a'
  background: '#13131b'
  on-background: '#e4e1ed'
  surface-variant: '#34343d'
typography:
  display-lg:
    fontFamily: Outfit
    fontSize: 48px
    fontWeight: '700'
    lineHeight: '1.1'
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Outfit
    fontSize: 32px
    fontWeight: '700'
    lineHeight: '1.2'
    letterSpacing: 0.05em
  headline-md:
    fontFamily: Outfit
    fontSize: 24px
    fontWeight: '600'
    lineHeight: '1.2'
    letterSpacing: 0.02em
  body-lg:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '400'
    lineHeight: '1.6'
  body-md:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: '1.6'
  label-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '600'
    lineHeight: '1.2'
    letterSpacing: 0.08em
  headline-lg-mobile:
    fontFamily: Outfit
    fontSize: 28px
    fontWeight: '700'
    lineHeight: '1.2'
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  base: 8px
  container-padding: 24px
  gutter: 16px
  stack-sm: 12px
  stack-md: 24px
  stack-lg: 48px
---

## Brand & Style
The design system embodies a "Cyber-Glass" aesthetic, merging high-stakes security with futuristic transparency. It is designed for an enterprise-level cyber-security environment where clarity and protection are paramount.

The visual narrative focuses on depth, light refraction, and precision. By using glassmorphic layers, the UI suggests an interface that is both advanced and lightweight, allowing the user to "see through" complex data while remaining protected by a solid, crystalline structure. The emotional response is one of calm control within a high-tech digital frontier.

**Style Keynote:**
- **Glassmorphism:** Multi-layered translucent surfaces with high-density backdrop blurs.
- **Cyber-Glow:** Subtle, neon-tinted "light leaks" that highlight interactive zones and status indicators.
- **Precision Typography:** A blend of geometric and systematic fonts to convey authority.

## Colors
The palette is rooted in deep-space tones to maximize the contrast of neon accents. The background uses a linear gradient from **#0A0A12** to **#120E22**, creating a sense of infinite digital depth.

**Primary (Cyber Cyan):** Used for critical data, active states, and "secure" status indicators.
**Secondary (Hyper Purple):** Used for AI features, encryption status, and premium navigation elements.
**Glass Surfaces:** All containers must use the defined glass surface with a minimum of 12px backdrop-blur. Borders are thin (1px) and use the glass border variable to simulate light catching the edge of a lens.

## Typography
The typography system balances the technical precision of **Inter** with the geometric modernism of **Outfit**. 

To accommodate the Vietnamese language, line-heights are set slightly wider (1.6 for body) to ensure stacked diacritics (e.g., "ế", "ổ") do not clash between lines. All headlines use uppercase styling with increased letter spacing to evoke a "HUD" (Heads-Up Display) aesthetic common in futuristic interfaces.

- **Headlines:** Should feel architectural and bold.
- **Body:** Focused on high legibility against dark, blurred backgrounds.
- **Labels:** Small, tracked-out, and bolded for a technical, metadata feel.

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

## Shapes
The shape language is "Advanced Geometric." While the environment is technical, the corners are softened to level 2 (0.5rem base) to provide a premium, modern feel that avoids the "aggressive" sharpness of traditional brutalist cyber-aesthetics.

- **Primary Containers:** 1rem (rounded-lg) for large dashboard cards.
- **Inputs & Buttons:** 0.5rem (base roundedness) to maintain a crisp, clickable feel.
- **System Icons:** Should follow a 2px stroke weight with rounded terminals to match the font geometry.

## Components
- **Buttons:** 
  - *Primary:* Solid Cyber Cyan with black text for maximum contrast. Include a subtle cyan outer glow.
  - *Secondary:* Ghost glass style with a Hyper Purple border.
- **Glass Cards:** Must include `backdrop-filter: blur(12px)` and a top-to-bottom subtle white-to-transparent border gradient to simulate a light source from above.
- **Input Fields:** Semi-transparent dark fill (rgba(0,0,0,0.3)) with a glass border. On focus, the border glows Cyber Cyan.
- **Status Chips:** High-saturation pills (Cyan for "Secure", Purple for "Scanning", Red for "Threat") with a 10% background opacity of the same color.
- **Progress Indicators:** Use glowing "scanning" animations—thin cyan lines that travel across the surface of glass cards to indicate activity.
- **Data Visualizations:** Use neon stroke lines with no fills, or subtle gradient fills that mirror the background glows.