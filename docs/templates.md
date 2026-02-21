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

1. **CLI `-o`** — explicit path or template string (rendered as Scriban)
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

Subdirectory structure is mirrored to the output. Both directory names and the `-o` path
support Scriban expressions — they are rendered before the output path is constructed:

```
templates/
  {{ solution }}/
    {{ solution }}.csproj.tmpl
  {{ solution }}UI/
    {{ solution }}UI.csproj.tmpl
```

```bash
st render templates/ --data data.yaml -o ".\Extensions\{{ solution }}\Src\"
# → .\Extensions\MyModule\Src\MyModule\MyModule.csproj
# → .\Extensions\MyModule\Src\MyModuleUI\MyModuleUI.csproj
```

> **Windows note:** avoid ending the `-o` path with `\"` — the backslash escapes the closing
> quote and produces a parse error. Use a path without a trailing separator, or end with `\\.`.

## Output to stdout or clipboard

```bash
st render template.txt --data data.yaml --stdout        # print to stdout
st render template.txt --data data.yaml --clip          # copy to clipboard
st render template.txt --data data.yaml --stdout --clip # both
```

In directory mode all rendered files are concatenated in `--stdout` / `--clip` output.
A confirmation message (`Copied to clipboard.`) is written to stderr so it does not
pollute piped output.
