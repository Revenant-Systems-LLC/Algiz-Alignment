using System.CommandLine;
using SageRage.Cli.Commands;

var rootCommand = new RootCommand("sage-rage — SAIGE/RAGE alignment pipeline CLI");

rootCommand.AddCommand(EvalCommand.Create());

rootCommand.SetHandler(() =>
{
    Console.WriteLine("SAGE-RAGE CLI");
    Console.WriteLine("Use --help to see available commands.");
});

return await rootCommand.InvokeAsync(args);
