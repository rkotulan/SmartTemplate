# Templates

SmartTemplate uses [Scriban](https://github.com/scriban/scriban) syntax.

## Basic syntax

```scriban
{{ name }}                                  # variable
{{ name | string.upcase }}                  # filter / pipe
{{ if active }}enabled{{ end }}             # conditional
{{ for item in items }}{{ item }}{{ end }}  # loop
```

## Date functions

```scriban
{{ date.now | date.to_string '%Y-%m-%d' }}                            # today
{{ date.now | date.add_days 7 | date.to_string '%d.%m.%Y' }}         # +7 days
{{ date.parse '2025-01-01' | date.add_months 3 | date.to_string '%Y-%m-%d' }}
```

## Output filename resolution

The output path is resolved in this order (first match wins):

1. **CLI `-o`** — explicit path or template string
2. **`output` key in the data file** — template string applied to every file
3. **Template filename itself** — strip `.tmpl`, render the rest as a template

Option 3 is the recommended approach: encode the desired filename and extension directly in the template's name.

```
templates/
  {{ project }}.md.tmpl               → MyProject.md
  {{ project }}_{{ version }}.md.tmpl → MyProject_1.0.0.md
```

## Directory mode

Render all `*.tmpl` files in a folder at once:

```bash
st render ./templates/ --data data.yaml -o ./output/
```

Subdirectory structure is mirrored to the output:

```
templates/
  Controllers/{{ entity }}Controller.cs.tmpl
  Dto/{{ entity }}Dto.cs.tmpl
```

```bash
st render templates/ --data data.yaml -o ./src/
# → src/Controllers/UserController.cs
# → src/Dto/UserDto.cs
```
