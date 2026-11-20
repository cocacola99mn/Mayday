# Floor Crawler — Art Style Guide

> **Target look:** *grim painted portrait*. A 3D world that reads like the rough, hand-painted character art from *Fear & Hunger*: dark, grotesque, high contrast, with visible brush strokes and dirty ink lines.

---

## 1. Visual Pillars

| Pillar | What it means | How we get it |
|---|---|---|
| **Painted, not rendered** | Flat brush patches, no smooth CG gradients | Kuwahara screen filter + brush-noise shading |
| **Hard, dirty shadows** | 2–4 tone steps; shadow edges are ragged, not clean cel lines | Banded lighting broken up by `BrushNoise` |
| **Tinted darkness** | Shadows are deep purple/blue, never neutral grey | `_ShadowColor` + split toning |
| **Ink outlines** | Dark, slightly uneven contour lines that "boil" | Inverted-hull outline with stepped jitter |
| **Edge light** | Pale sliver of light along silhouettes (fur, shoulders, faces) | Noisy rim light |
| **Grim grade** | Desaturated, contrasty, crushed blacks, grain, vignette | URP Volume post-processing |

**One-line test:** a screenshot should look like a dirty oil painting under candlelight, not like a game engine.

---

## 2. Color Palette

| Role | Color | Hex | Notes |
|---|---|---|---|
| Shadow tint | Deep bruise purple | `#1A0F24` | Default `_ShadowColor` |
| Outline | Near-black violet | `#050305` | Never pure black |
| Rim / highlight | Bone white | `#F2E6D9` | Warm, slightly dirty |
| Split-tone shadows | Dusk purple | `#59337F` | Volume |
| Split-tone highlights | Old-paper yellow | `#BFA666` | Volume |
| Vignette | Ink black-purple | `#050008` | Volume |

**Rules**
- Keep saturation low across the board. Use strong color only for focal points like blood, eyes, a flame or the grin.
- Skin should look sickly: pale, greyish, purple or green undertones.
- Avoid pure white and pure black. Everything should look slightly dirty.

---

## 3. Rendering Stack

```
Hand-painted albedo ──► GrimPainted.shader ──► PaintKuwahara (full-screen) ──► Volume post-FX ──► Screen
   (texture art)         (banding, brush,        (oil-paint patches,            (grade, grain,
                          rim, outline)            canvas grain)                  vignette)
```

### 3.1 Object shader — `Custom/GrimPainted`
Location: `Assets/FearHungerStyle/Shaders/GrimPainted.shader`

| Property | Default | Effect | Tips |
|---|---|---|---|
| `_BaseMap` | — | Hand-painted albedo | Does most of the work. See §4 |
| `_ShadowColor` | `#1A0F24` | Multiplies albedo on the shadow side | Push it more purple for unnatural, more blue for cold |
| `_Bands` | 3 | Number of light steps | 2 = harsh/graphic, 3 = default, 4+ = softer |
| `_Ambient` | 0.08 | Lifts shadow side | Keep low; darkness is the mood |
| `_BrushTex` | `BrushNoise.png` | Triplanar stroke noise | Must be **Repeat**, linear (non-sRGB) |
| `_BrushScale` | ≈ 6 / object height | Stroke size in world space | Smaller value = bigger strokes |
| `_BrushStrength` | 0.4 | How much strokes break the shadow edge | 0.3–0.6 looks painted; >0.7 looks noisy |
| `_RimColor` / `_RimPower` / `_RimStrength` | bone / 3.5 / 0.5 | Edge light | Raise strength for hero characters |
| `_OutlineColor` | `#050305` | Ink color | — |
| `_OutlineWidth` | ≈ 0.004 × object height | World-space thickness | Thinner on props, thicker on characters |
| `_OutlineJitter` | 0.4 | Line wobble | 0 = clean line |
| `_OutlineFPS` | 8 | Boil rate | 6–12 feels hand-animated; 0 = static |

**Additional lights** (torches, lanterns) get the same banding, so they make hard pools of light.

### 3.2 Screen filter — `Hidden/Custom/PaintKuwahara`
Location: `Assets/FearHungerStyle/Shaders/PaintKuwahara.shader`
Material: `Assets/FearHungerStyle/Materials/PaintKuwahara.mat`
Hooked up as a **Full Screen Pass Renderer Feature** on `PC_Renderer` (Before Rendering Post Processing, fetches color).

| Property | Default | Effect |
|---|---|---|
| `_Radius` | 3 | Paint patch size (cost grows with radius²) |
| `_Spacing` | 1.5 | Spreads samples. A cheap way to get bigger strokes |
| `_CanvasTex` | `CanvasGrain.png` | Paper/canvas texture |
| `_CanvasStrength` | 0.25 | Canvas visibility |
| `_CanvasScale` | 2 | Canvas tiling |

> ⚠️ Only on `PC_Renderer`. Add it to `Mobile_Renderer` too if that quality level gets used.

### 3.3 Post-processing — `Assets/Settings/SampleSceneProfile.asset`

| Override | Settings |
|---|---|
| Tonemapping | ACES |
| Color Adjustments | Post Exposure 0.5, Contrast +25, Saturation −35 |
| Split Toning | Shadows `#59337F`, Highlights `#BFA666`, Balance −20 |
| Lift Gamma Gain | Lift −0.04 (crush blacks) |
| Vignette | Intensity 0.4, Smoothness 0.45, Color `#050008` |
| Film Grain | Medium3, Intensity 0.45, Response 0.7 |
| Chromatic Aberration | 0.12 |
| Bloom | Intensity 0.3 (subtle) |

---

## 4. Texture Art Guidelines

The shaders can only push a look that the textures already have.

- **Paint, don't photo-source.** Use rough brushes (Substance Painter painterly brushes, Krita, Blender texture paint). Leave visible strokes.
- **Bake light into the texture.** Paint AO, grime in creases and a hint of top-down light. Darken the undersides.
- **Low texel density is fine.** 512–1024 px for characters. Chunky paint suits the style.
- **No PBR detail.** Skip normal maps for small details and skip specular/metallic. Paint the shape instead.
- **Focal contrast.** The face, grin and hands get the most value contrast. Clothing and background stay muddy.
- **Grime layer.** A dirty, desaturated overlay on everything ties the palette together.

## 5. Model & Character Guidelines

- **Exaggerate proportions:** long limbs, narrow torsos, oversized grins, hands and heads.
- **Silhouette first.** Characters must read as a black shape, because they often will be one.
- **Smooth normals for outlines.** Hard-edged meshes split the inverted hull. Use smoothed normals (or a baked smooth-normal channel) on characters.
- **Mid-poly.** Shading detail comes from paint, not geometry.

## 6. Lighting Guidelines (to do)

- One strong key light from a dramatic angle: side, below or behind the subject.
- Very dark ambient (dark purple). No bright skybox in gameplay areas.
- Black or dark-purple exponential fog to swallow distance.
- Warm, flickering point lights (torches, candles) as the main light sources in interiors.
- Let most of the frame fall into shadow. Light is a reward.

---

## 7. Do / Don't

| ✅ Do | ❌ Don't |
|---|---|
| Ragged, painted shadow edges | Clean anime cel shading |
| Purple/blue tinted darkness | Neutral grey shadows |
| Muted palette with one accent | Saturated, colorful scenes |
| Grain, vignette, dirt | Clean, glossy, specular surfaces |
| Grotesque, exaggerated shapes | Realistic proportions |
| Darkness hiding most of the frame | Evenly lit rooms |

---

## 8. Performance Notes

- Kuwahara costs about `4 × (Radius+1)²` samples per pixel (64 at Radius 3). Raise `_Spacing` before you raise `_Radius`.
- The outline is an extra draw per object. Use it on characters and key props only; give background props a plain material.
- `GrimPainted` uses `UsePass` for shadow and depth, so it's **not SRP Batcher compatible**. If you have many objects, replace those passes with a custom ShadowCaster/DepthOnly.

## 9. Files

```
Assets/FearHungerStyle/
├── Shaders/
│   ├── GrimPainted.shader       # object shader
│   └── PaintKuwahara.shader     # full-screen oil-paint filter
├── Textures/
│   ├── BrushNoise.png           # tileable stroke noise (Repeat, linear)
│   └── CanvasGrain.png          # tileable canvas grain
├── Materials/
│   ├── Char_sinh_nhat_Grim.mat  # main character
│   └── PaintKuwahara.mat        # screen filter material
└── ArtStyle.md                  # this guide
```

**Adding a new asset:** create a material with `Custom/GrimPainted`, assign its painted albedo, then set `_BrushScale ≈ 6 / height` and `_OutlineWidth ≈ 0.004 × height`.
