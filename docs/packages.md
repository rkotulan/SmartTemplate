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
| `output` | no | Output file or directory (supports Scriban expressions) |
| `stdout` | no | Write to stdout instead of files (default: `false`) |
| `clip` | no | Copy output to clipboard (default: `false`) |
| `no_interactive` | no | Skip prompts — for CI/scripting (default: `false`) |
| `plugins` | no | Plugin directory or named global plugin |
| `vars` | no | Additional `key=value` overrides (list) |

All paths (`data`, `templates`, `output`) are relative to the directory containing `packages.yaml`.

## Usage

```bash
# Interactive selection
st run

# Run a specific package directly
st run 3d
st run fr

# Use a different config file
st run --config path/to/packages.yaml
st run 3d --config path/to/packages.yaml
```

**Interactive session example:**

```
Available packages:
  1) 3D Module Generator  [3d]
  2) FillFormRelations  [fr]
Select package (1-2): 1

Zadejte hodnoty proměnných:
Solution name [MyModule]: OrderModule
...
```

## Recommended folder layout

```
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

From the `.st/` folder simply run:

```bash
st run
```
