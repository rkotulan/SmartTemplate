# SmartTemplate

A lightweight CLI template renderer powered by [Scriban](https://github.com/scriban/scriban).
Write templates with variables, loops, and date expressions — feed them YAML or JSON data and get rendered files in seconds.

## Install

```bash
dotnet tool install -g SmartTemplate
```

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
st render <input> [--data <file>] [-o <output>] [--var key=value]...
                  [--ext .tmpl] [--no-interactive] [--plugins <dir>] [--stdout]
```

| Option | Description |
|--------|-------------|
| `<input>` | Template file or directory of `*.tmpl` files |
| `--data` | YAML or JSON data file |
| `-o` / `--output` | Output file or directory (supports template expressions) |
| `--var key=value` | Inline variable (repeatable, highest priority) |
| `--ext` | Template extension for directory scan (default: `.tmpl`) |
| `--no-interactive` | Skip prompts — for CI / scripting |
| `--plugins` | Directory containing plugin assemblies (`*.dll`) |
| `--stdout` | Write rendered output to stdout instead of files |

## Documentation

- [Templates — syntax, date functions, directory mode, output resolution](docs/templates.md)
- [Interactive prompts — types, date formats, examples](docs/prompts.md)
- [Plugins — NuGet install, local plugins, writing your own](docs/plugins.md)

## Requirements

- .NET 10 SDK
