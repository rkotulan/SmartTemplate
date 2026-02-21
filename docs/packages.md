# Package bundles (`st run`)

A **package** is a named set of templates, a data file, and output options.
Define all your packages in a single `packages.yaml` and use `st run` to pick and execute one interactively.

## packages.yaml format

```yaml
packages:
  - id: 3d
    name: 3D Module Generator
    data: 3d-data.yaml
    templates: templates/3d/
    output: "X:\\Extensions\\{{ solution }}\\Src\\Modules3D\\"

  - id: fr
    name: FillFormRelations
    data: fr-data.yaml
    templates: templates/fr/FillFormRelations.cs.tmpl
    stdout: true
    clip: true
```

| Field | Required | Description |
|-------|----------|-------------|
| `id` | yes | Short identifier used on the command line |
| `name` | no | Human-readable label shown in the interactive menu |
| `data` | no | YAML/JSON data file path |
| `templates` | yes | Template file or directory path |
| `output` | no | Output file or directory (supports Scriban expressions). When omitted, files are written to the directory where `st run` was invoked. |
| `stdout` | no | Write to stdout instead of files (default: `false`) |
| `clip` | no | Copy output to clipboard (default: `false`) |
| `no_interactive` | no | Skip prompts — for CI/scripting (default: `false`) |
| `plugins` | no | Plugin directory or named global plugin |
| `vars` | no | Additional `key=value` overrides (list) |

Paths `data` and `templates` are relative to the directory containing `packages.yaml`.
The `output` path (when set) is also relative to `packages.yaml`; when omitted, output goes to the **invocation directory** — the working directory from which `st run` was called.

## Config file discovery

When `--config` is not specified, `st run` searches for `packages.yaml` in this order:

1. `packages.yaml` in the **current directory** (backward-compatible fast path)
2. `.st/packages.yaml` in the **current directory**
3. `.st/packages.yaml` in the **parent directory** — keeps walking up until found or the filesystem root is reached

This means you can place your `packages.yaml` inside a `.st/` subdirectory anywhere in the project tree and invoke `st run` from any subdirectory of the project — it will find the config automatically.

## Usage

```bash
# Interactive selection (auto-discovers packages.yaml / .st/packages.yaml)
st run

# Run a specific package directly
st run 3d
st run fr

# Explicit config file
st run --config path/to/packages.yaml
st run 3d --config path/to/packages.yaml
```

**Interactive session example:**

```
Available packages:
  1) 3D Module Generator  [3d]
  2) FillFormRelations  [fr]
  3) Exit
Select package (1-3): 1

Zadejte hodnoty proměnných:
Solution name [MyModule]: OrderModule
...
```

## Recommended folder layout

Store your templates and config in a `.st/` subdirectory at the project root:

```
MyProject/
  src/
  tests/
  .st/
    packages.yaml
    3d-data.yaml
    fr-data.yaml
    templates/
      3d/
        {{ solution }}/
          {{ solution }}.csproj.tmpl
      fr/
        FillFormRelations.cs.tmpl
```

You can invoke `st run` from anywhere inside `MyProject/` — even from a deeply nested subdirectory — and SmartTemplate will walk up and find `.st/packages.yaml`.
When no `output` is set in a package definition the rendered files land in the directory you were in when you ran `st run`, not in `.st/`.

```bash
# From any subdirectory of the project:
cd MyProject/src/SomeModule
st run          # finds MyProject/.st/packages.yaml, writes output to MyProject/src/SomeModule/
st run 3d       # same, runs the '3d' package directly
```
