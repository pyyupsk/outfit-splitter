# Outfit Splitter

Split VRChat avatar outfits by mesh with PhysBone collider preservation.

## Features

- **Mesh-based separation** - Splits SkinnedMeshRenderers by material / sub-mesh / mesh name
- **Bone pruning** - Removes unused bones from separated pieces automatically (recursive from rootBone)
- **PhysBone preservation** - Maintains VRCPhysBone and VRCPhysBoneCollider setup on separated pieces (when VRChat SDK detected)
- **Partial mode** - Separate only selected meshes, keep rest intact
- **Undo support** - Full Unity Undo integration for safe experimentation
- **SDK optional** - Works without VRChat SDK (PhysBone features disabled if not installed)

## Installation

### Via VPM (Recommended)

**Option A: GitHub Releases only (no listing repo needed)**
1. Open VRChat Creator Companion
2. Settings → Packages → Add Repository
3. Add: `https://github.com/pyyupsk/outfit-splitter/releases`
4. Search "Outfit Splitter" and install

**Option B: Dedicated VPM Listing Repository (recommended for multiple packages)**
1. Create a new repo from [template-package-listing](https://github.com/vrchat-community/template-package-listing/generate)
2. Name it `outfit-splitter-listing` (or similar)
3. Edit `source.json` and add your package repo to `githubRepos`:
   ```json
   "githubRepos": ["pyyupsk/outfit-splitter"]
   ```
4. Enable GitHub Pages on the listing repo (Settings → Pages → Source: GitHub Actions)
5. In VCC, add: `https://pyyupsk.github.io/outfit-splitter-listing/index.json`

### Manual
Download latest `.unitypackage` from [Releases](https://github.com/pyyupsk/outfit-splitter/releases) and import.

## Usage

1. Open **Tools → Outfit Splitter v0.0.1**
2. Select avatar root or specific SkinnedMeshRenderers (2+ required to split)
3. Choose separation mode:
   - **By Material** - One piece per material slot on each renderer
   - **By Sub-mesh** - One piece per sub-mesh index on each renderer
4. Toggle options:
   - **Prune Unused Bones** - Remove bones not influencing the piece's mesh
   - **Preserve PhysBone Setup** - Copy VRCPhysBone and VRCPhysBoneCollider components
4. Click **Split Outfit**
5. Result:
   - Original avatar root **disabled** (e.g., `VFZA_Airi/`)
   - New pieces created (e.g., `VFZA_Airi-(M1)Jacket/`, `VFZA_Airi-(M2)Bikini_Top/`) with pruned bone hierarchies

## Requirements

- Unity 2022.3+
- VRChat SDK3 Avatars 3.10+ (optional, for PhysBone features)

## GitHub Actions

This repository includes:
- **`.github/workflows/release.yml`** - Builds `.zip` + `.unitypackage` on manual trigger (Actions tab → Build Release). Creates a GitHub Release with the package version as tag.

For VPM listing with multiple packages, create a separate **listing repository** from [template-package-listing](https://github.com/vrchat-community/template-package-listing/generate) and use `.github/workflows/build-listing.yml` there.

## License

MIT - see [LICENSE](../../LICENSE)