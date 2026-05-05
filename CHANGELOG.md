# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Spec `docs/specs/2026-05-05-vr-interaction-stack.md` (official-alignment contracts, SDD/TDD/CI/DI, Alyx tuning notes).
- `VRLogic`: `GrabInteractionRules`, `LocomotionWishRules`, `VrInteractionConstants`, `AlyxFeelTuningDefaults` for testable grab/locomotion rules and tuning defaults.
- `GripReleaseNotification` and `GrabNetworkContracts` for swappable release broadcast and multiplayer ownership notes.
- `.gitlab-ci.yml` job `unit_tests` running `dotnet test` on `UnitTests/testbed.unittest.csproj`.
- Unit tests `GrabInteractionRulesTests`, `LocomotionWishRulesTests`.
- Docs `docs/CI_UNIT_TEST.md` with the local/CI unit test command.
- `VRGhostHandTarget` component: non-physics ghost hand / joint target aligned to VR grip or optional `weapon_hold` attachment; `test.vr.scene` includes `GhostTarget_Left` and `GhostTarget_Right`.
- `VRPlayerRig.EnableGhostTargets` toggles all `VRGhostHandTarget` under the player hierarchy.
- Establish engineering workflow documents for SDD, TDD, CI/CD, DI, and command standards.
- `VRPlayerRig` root component to toggle locomotion, desktop hand fallback, and per-hand `VRGrabber` from the player root.
- `VRLogic` class library with `VRInteractionRules` (socket id and distance checks) and `VRLogic.UnitTests` MSTest project.
- Specification `docs/specs/2026-05-04-vr-player-rig.md`.

### Changed
- `VRGrabber`: grab/release runs in `OnFixedUpdate` (physics-step aligned); exposes `GrabInteractorState` and configurable grip thresholds; default attachment name uses `VrInteractionConstants`; release events go through `GripReleaseNotification`.
- `VRPlayerController`: movement, jump, and optional crouch run in `OnFixedUpdate` using `CharacterController` friction/acceleration (`ApplyFriction`/`Accelerate`/`Punch`); planar wish uses `LocomotionWishRules`; right-stick / snap turn remains on `OnUpdate` via `EnableRightStickTurn`.
- `VRGhostHandTarget` / `VRSocket` class docs clarify presentation vs interactor vs socket roles.
- `docs/specs/2026-05-04-vr-player-rig.md` documents `VRGhostHandTarget` and `VRController.Transform` / `AimTransform` for pose sourcing.
- `VRSocket` delegates id/radius checks to `VRInteractionRules`.
- `Assets/Scenes/Tests/test.vr.scene` wires `VRPlayerRig`, both-hand `VRGrabber`, and a `Grabbable` + `Rigidbody` on the test cube.
- `.sbproj` `ControlModes` now enables `Keyboard` and `Gamepad` alongside `VR` so desktop and VR can run from the same project configuration.

### Fixed
- N/A

### Removed
- N/A

## Changelog Policy

- Every code change must include an update in `CHANGELOG.md`.
- Add entries under `## [Unreleased]` using one of: `Added`, `Changed`, `Fixed`, `Removed`.
- Keep each bullet focused on impact and behavior, not implementation detail.
- At release time, move `Unreleased` items to a versioned section, for example `## [0.2.0] - 2026-04-30`.
