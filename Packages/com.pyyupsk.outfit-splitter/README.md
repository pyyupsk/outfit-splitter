# Outfit Splitter

Split VRChat avatar outfits by mesh with PhysBone collider preservation.

## Features

- **Mesh-based separation** - Splits SkinnedMeshRenderers by sub-mesh or material
- **Bone pruning** - Removes unused bones from separated pieces automatically
- **PhysBone preservation** - Maintains VRCPhysBone and VRCPhysBoneCollider setup on separated pieces
- **Partial mode** - Separate only selected meshes, keep rest intact
- **Undo support** - Full Unity Undo integration for safe experimentation

## Installation

### Via VPM (Recommended)
1. Open VRChat Creator Companion
2. Settings → Packages → Add Repository
3. Add: `https://pyyupsk.github.io/outfit-splitter/index.json`
4. Search "Outfit Splitter" and install

### Manual
Download latest `.unitypackage` from [Releases](https://github.com/pyyupsk/outfit-splitter/releases) and import.

## Usage

1. Open **VRChat SDK → Outfit Splitter v1.0.0**
2. Select avatar root or specific SkinnedMeshRenderers
3. Choose separation mode:
   - **By Material** - One piece per material slot
   - **By Sub-mesh** - One piece per sub-mesh index
4. Click **Split Outfit**
5. Review generated pieces in scene hierarchy

## Requirements

- Unity 2022.3+
- VRChat SDK3 Avatars 3.10+

## License

MIT - see [LICENSE](../../LICENSE)