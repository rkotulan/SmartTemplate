# SmartTemplate

A lightweight CLI template renderer powered by [Scriban](https://github.com/scriban/scriban).
Write templates with variables, loops, and date expressions — feed them YAML or JSON data and get rendered files in seconds.

## Install

```bash
dotnet tool install -g SmartTemplate
```

To update:

```bash
dotnet tool update -g SmartTemplate
```

## Quick start

```
# template.txt
Hello, {{ name }}!
Today is {{ date.now | date.to_string '%d.%m.%Y' }}.
```

```bash
st render template.txt --var name=World -o hello.txt
# → Hello, World!
#   Today is 20.02.2026.
```

## Usage

```
st render <input> [--data <file>] [-o <output>] [--var key=value]... [--ext .tmpl] [--no-interactive]
```

| Argument / Option | Description |
|---|---|
| `<input>` | Template file or directory of `*.tmpl` files |
| `--data` | YAML or JSON data file |
| `-o` / `--output` | Output file or directory (may itself be a template string) |
| `--var key=value` | Inline variable overrides (repeatable, highest priority) |
| `--ext` | Template extension when scanning a directory (default: `.tmpl`) |
| `--no-interactive` | Skip interactive prompts — for CI / scripting |

## Data files

Variables can come from a YAML or JSON file:

```yaml
# data.yaml
name: Alice
version: 1.0.0
output: "release_{{ version }}.txt"
```

```bash
st render template.txt --data data.yaml
# output filename is read from data["output"]: release_1.0.0.txt
```

`--var` overrides always win:

```bash
st render template.txt --data data.yaml --var version=2.0.0
# → release_2.0.0.txt
```

## Output filename resolution

The output path is resolved in this order (first match wins):

1. **CLI `-o`** — explicit path or template string
2. **`output` key in data file** — template string applied to all files
3. **Template filename itself** — strip `.tmpl`, render the rest as a template

The third option is the recommended approach for directory mode: encode both the desired
filename and extension directly in the template file's name.

```
templates/
  {{ project }}.md.tmpl          → MyProject.md
  {{ project }}_{{ version }}.md.tmpl  → MyProject_1.0.0.md
```

Each file is rendered independently, so every template must define all variables it uses.

## Interactive prompts

Add a `prompts` key to your data file and the tool will ask for values at runtime:

```yaml
# data.yaml
prompts:
  - name: project_name
    label: Project name
    default: MyProject
  - name: version
    label: Version
  - name: release
    label: Include release notes?
    type: bool
    default: "yes"
```

```
$ st render template.txt --data data.yaml
Zadejte hodnoty proměnných:
Project name [MyProject]:
Version: 2.1.0
Include release notes? [y/n] (výchozí: yes): n
→ MyProject_2.1.0.txt
```

Suppress prompts for CI:

```bash
st render template.txt --data data.yaml --no-interactive
# uses defaults from the prompts definitions
```

### Prompt types

| `type` | Description |
|---|---|
| `string` | Plain text (default) |
| `int` | Integer number |
| `bool` | Boolean — accepts `y`, `yes`, `true`, `1`, `ano`, `a` |
| `date` | Date — normalized to `yyyy-MM-dd` for use with `date.parse` in templates |

### Date prompts

Type `date` accepts the following input formats and always normalizes the value to
`yyyy-MM-dd` so Scriban's `date.parse` works reliably:

| Input | Stored as |
|---|---|
| `24.2.2026` | `2026-02-24` |
| `24.02.2026` | `2026-02-24` |
| `2026-02-24` | `2026-02-24` |
| `2026/02/24` | `2026-02-24` |

To accept a different format use the optional `format` key (standard .NET format string):

```yaml
prompts:
  - name: start_date
    label: "Začátek"
    type: date
    format: "M/d/yyyy"
    default: "2/24/2026"
```

The `format` is tried first; if it doesn't match, the built-in formats above are tried as fallback.

### Example: date from prompt, formatted with offsets

`data.yaml`:

```yaml
prompts:
  - name: start_date
    label: "Start date (YYYY-MM-DD)"
    default: "2026-02-20"
```

`{{ start_date }}.md.tmpl`:

```scriban
{{ d = date.parse start_date }}
Day 1: {{ d | date.to_string '%m.%d.%Y' }}
Day 2: {{ d | date.add_days 1 | date.to_string '%m.%d.%Y' }}
Day 3: {{ d | date.add_days 2 | date.to_string '%m.%d.%Y' }}
```

```
$ st render ./templates/ --data data.yaml
Start date (YYYY-MM-DD) [2026-02-20]:
→ 2026-02-20.md
```

## Template syntax

SmartTemplate uses [Scriban](https://github.com/scriban/scriban) syntax.

```scriban
{{ name }}                                  # variable
{{ name | string.upcase }}                  # filter
{{ if active }}enabled{{ end }}             # conditional
{{ for item in items }}{{ item }}{{ end }}  # loop
```

### Date functions

```scriban
{{ date.now | date.to_string '%Y-%m-%d' }}           # today's date
{{ date.today }}                                      # date without time
{{ date.now | date.add_days 7 | date.to_string '%d.%m.%Y' }}
{{ date.parse '2025-01-01' | date.add_months 3 | date.to_string '%Y-%m-%d' }}
```

## Directory mode

Render all `*.tmpl` files in a folder at once:

```bash
st render ./templates/ --data data.yaml -o ./output/
```

Each template's output path is resolved independently. Template filenames support
Scriban expressions — name your files like `{{ krok }}.md.tmpl` to get dynamic output names.

## Merge order

Later sources overwrite earlier ones:

1. YAML / JSON data file
2. Interactive prompt values
3. `--var` CLI overrides

## Build & test

```bash
dotnet build /nodeReuse:false
dotnet test  /nodeReuse:false
```

## Requirements

- .NET 10 SDK
