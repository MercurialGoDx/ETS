# Global Agent Rules

Canonical cross-project rules for game development under `F:\VIBECODE`.
The package contains a universal agent contract plus a lean Unity-specific
workflow. Project rules stay small and point back here instead of copying the
whole guide.

## Contents

- `AGENTS.md` — universal game-development workflow and Unity verification
  policy.
- `code-style.md` — fallback C# style for projects without local conventions.
- `pipelines/unity-cli.md` — official Unity CLI discovery and preflight.
- `pipelines/unity-testing-asmdefs.md` — test selection and minimal assemblies.
- `skills/unity-game-agent/` — installable lean Unity skill.
- `templates/` — project AGENTS, development-plan, and asmdef templates.
- `scripts/` — local skill installer and package validation.

## New Project Bootstrap

Add this repository to the new project as
`.agents/global-agent-rules`. On this machine the canonical local source can be
used as a submodule:

```powershell
git submodule add F:\VIBECODE\_meta\global-agent-rules .agents/global-agent-rules
```

Then:

1. Copy `templates/AGENTS.unity.md` to the project root as `AGENTS.md` for a
   Unity project and fill only the detected project facts and real overrides.
2. Read the shared files in the order declared by that template.
3. Inspect the existing project before adding folders, packages, assemblies, or
   documents.
4. For a new/empty Unity project, use the asmdef templates only when the project
   is ready for assembly boundaries. Existing projects keep their current graph.
5. Create `gamedesign/DEVELOPMENT_PLAN.md` from the template only for work that
   genuinely needs multiple stages.

Reusable prompt:

```text
Read .agents/global-agent-rules/README.md and prepare this project according to
the shared rules. Detect the current stack first, preserve existing conventions,
show the resulting setup and verification, and do not install optional
dependencies without a concrete need.
```

## Local Skill

Install the repository-owned skill once per machine from this canonical checkout:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\install-local-skill.ps1
```

If another `unity-game-agent` directory already exists, inspect it first and
rerun with `-Force` only when replacing it is intended. Restart Codex after
changing installed skills.

## Rule Precedence

Use this order:

1. explicit human instruction;
2. project-root `AGENTS.md` and documented project decisions;
3. shared `AGENTS.md`;
4. the Unity skill and shared code style;
5. tool defaults.

Project-specific facts belong in the project. Universal repeatable guidance
belongs here.
