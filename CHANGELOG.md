# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Establish engineering workflow documents for SDD, TDD, CI/CD, DI, and command standards.
- `VRPlayerRig` root component to toggle locomotion, desktop hand fallback, and per-hand `VRGrabber` from the player root.
- `VRLogic` class library with `VRInteractionRules` (socket id and distance checks) and `VRLogic.UnitTests` MSTest project.
- Specification `docs/specs/2026-05-04-vr-player-rig.md`.

### Changed
- `VRPlayerController` exposes `EnableRightStickTurn`; when disabled, only joystick locomotion runs (replacing the removed `VRMovement` component).
- `VRSocket` delegates id/radius checks to `VRInteractionRules`.
- `Assets/Scenes/Tests/test.vr.scene` wires `VRPlayerRig`, both-hand `VRGrabber`, and a `Grabbable` + `Rigidbody` on the test cube.

### Fixed
- N/A

### Removed
- N/A

## Changelog Policy

- Every code change must include an update in `CHANGELOG.md`.
- Add entries under `## [Unreleased]` using one of: `Added`, `Changed`, `Fixed`, `Removed`.
- Keep each bullet focused on impact and behavior, not implementation detail.
- At release time, move `Unreleased` items to a versioned section, for example `## [0.2.0] - 2026-04-30`.
