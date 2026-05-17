using System.CommandLine;

var rootCommand = new RootCommand("sage-rage — SAIGE/RAGE alignment pipeline CLI");

rootCommand.SetHandler(() =>
{
    Console.WriteLine("SAGE-RAGE CLI");
    Console.WriteLine("Use --help to see available commands.");
});

return await rootCommand.InvokeAsync(args);
