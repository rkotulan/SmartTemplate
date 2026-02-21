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
                  [--ext .tmpl] [--no-interactive] [--plugins <dir>] [--stdout] [--clip]
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
| `--clip` | Copy rendered output to clipboard (combinable with `--stdout`) |

## Package bundles

Run one of several named template sets from a single `packages.yaml`:

```bash
st run           # interactive selection
st run 3d        # run package by ID directly
```

Place `packages.yaml` inside a `.st/` subdirectory at the project root and call `st run` from any subfolder — SmartTemplate walks up the directory tree to find it automatically.
When `output` is not set in a package definition, rendered files are written to the directory where `st run` was invoked.

See [docs/packages.md](docs/packages.md) for the full `packages.yaml` reference.

## Plugin commands

```
st plugin install <package> [--version <v>] [--source <url>] [--prerelease]
st plugin update  [<package>]
st plugin list
st plugin uninstall <package>
```

## Documentation

- [Templates — syntax, date functions, directory mode, output resolution](docs/templates.md)
- [Interactive prompts — types, date formats, examples](docs/prompts.md)
- [Plugins — NuGet install, update, local plugins, writing your own](docs/plugins.md)
- [Package bundles — packages.yaml format, st run usage](docs/packages.md)

## Requirements

- .NET 10 SDK
