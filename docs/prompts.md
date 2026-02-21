# Interactive prompts

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
```

Suppress prompts for CI:

```bash
st render template.txt --data data.yaml --no-interactive
```

## Prompt types

| `type`    | Description |
|-----------|-------------|
| `string`  | Plain text (default) |
| `int`     | Integer number |
| `bool`    | Boolean — accepts `y`, `yes`, `true`, `1`, `ano`, `a` |
| `date`    | Date — normalized to `yyyy-MM-dd` for use with `date.parse` in templates |

## Date prompts

Type `date` accepts the following input formats and always normalizes the value to
`yyyy-MM-dd` so Scriban's `date.parse` works reliably:

| Input        | Stored as    |
|--------------|--------------|
| `24.2.2026`  | `2026-02-24` |
| `24.02.2026` | `2026-02-24` |
| `2026-02-24` | `2026-02-24` |
| `2026/02/24` | `2026-02-24` |

To accept a custom format use the optional `format` key (standard .NET format string):

```yaml
prompts:
  - name: start_date
    label: Start date
    type: date
    format: "M/d/yyyy"
    default: "2/24/2026"
```

The `format` is tried first; built-in formats above are used as fallback.

### Example: date from prompt, formatted with day offsets

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
