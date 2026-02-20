using System.CommandLine;
using SmartTemplate.Cli.Commands;

var rootCommand = new RootCommand("SmartTemplate — Jinja2-like CLI template generator powered by Scriban");
rootCommand.Subcommands.Add(RenderCommand.Build());

return await rootCommand.Parse(args).InvokeAsync();
