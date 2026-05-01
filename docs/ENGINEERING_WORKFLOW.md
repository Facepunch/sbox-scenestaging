# Engineering Workflow Standard

This project uses a specification-first process:

1. SDD (Specification-Driven Development)
2. TDD (Test-Driven Development)
3. CI/CD guardrails
4. DI (Dependency Injection) for testability and flexibility
5. Mandatory changelog updates

Do not start implementation until the related specification is approved.

## 1) SDD: Spec First

Before writing production code, create a spec file in `docs/specs/`.

Required sections:

- Context and problem statement
- Goals and non-goals
- Scope (in/out)
- Functional requirements
- Non-functional requirements (performance, reliability, observability)
- API/component contracts
- Test strategy (unit/integration/e2e)
- Rollout plan and rollback plan
- Risks and open questions
- Definition of done

Naming convention:

- `docs/specs/YYYY-MM-DD-<feature-name>.md`

## 2) TDD: Tests Before Implementation

For each requirement:

1. Write or update failing test(s) first.
2. Implement minimal code to pass tests.
3. Refactor while keeping tests green.

Test requirements:

- Unit tests for business logic
- Integration tests for boundaries (IO, scene lifecycle, external services)
- Regression test for each fixed bug

Minimum merge gate:

- New/changed behavior is covered by tests.
- Existing tests remain green.

## 3) CI/CD Baseline

Every merge request should pass:

- Build
- Unit tests
- Lint/style checks (if enabled)
- Optional smoke/integration checks

CI pipeline principles:

- Fast feedback first (lint + unit tests early)
- Deterministic steps (pin tool versions where possible)
- Fail fast and stop downstream stages on critical failures

## 4) DI Standard

Apply dependency injection to improve testability:

- Depend on interfaces/abstractions, not concrete implementations.
- Inject collaborators via constructor or explicit setup entry points.
- Avoid hidden global state in core logic.
- Keep game/engine framework glue thin; isolate pure logic.

DI checklist:

- Can this component be tested without full runtime scene boot?
- Are external dependencies mockable/stubbable?
- Is object creation centralized and controlled?

## 5) Changelog Discipline

Each change must update `CHANGELOG.md` under `Unreleased`.

- `Added`: new capability
- `Changed`: behavior or contract update
- `Fixed`: bug fix
- `Removed`: removed feature or API

Do not merge code changes without changelog entry.

## 6) Pull Request Checklist

- Spec exists and is up to date
- Tests added/updated first and now passing
- DI rules respected in touched code
- CI checks pass
- `CHANGELOG.md` updated
