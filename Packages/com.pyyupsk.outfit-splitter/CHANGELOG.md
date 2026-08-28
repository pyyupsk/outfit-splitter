# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.1] - 2026-08-28

### Added
- Initial release (v0.0.1)
- Mesh-based outfit separation (by material / sub-mesh / mesh name)
- Instantiate full avatar hierarchy per piece, prune unwanted meshes
- Recursive bone pruning from rootBone
- Preserves necessary bones + PhysBone colliders/children + parent chains
- VRCPhysBone / VRCPhysBoneCollider preservation (when VRChat SDK detected)
- Partial separation mode (selected meshes only)
- Full Undo support (grouped operations)
- Menu integration under `Tools/Outfit Splitter`
- SDK detection via reflection (no define constraints needed)
- Auto-disables original avatar root after split

### Changed
- Uses instantiate + prune approach (from reference implementation)
- Removed complex bone weight remapping / bindpose reconstruction
- Window title shows version: "Outfit Splitter v0.0.1"

### Fixed
- Bone index out of range errors
- Transform destroyed errors during bone pruning
- Compile without VRChat SDK installed

## [Unreleased]

### Added
- None yet

### Changed
- None yet

### Fixed
- None yet