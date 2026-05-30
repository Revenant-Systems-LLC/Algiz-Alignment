using System.CommandLine;
using SageRage.Cli.Evaluation;

namespace SageRage.Cli.Commands;

public static class EvalCommand
{
    public static Command Create()
    {
        var datasetOption = new Option<string>(
            name: "--dataset",
            description: "Path to the JSON evaluation dataset")
        {
            IsRequired = true
        };

        var outputOption = new Option<string>(
            name: "--output",
            description: "Path to write the output JSON report",
            getDefaultValue: () => "evaluation_results.json");

        var command = new Command("eval", "Run the A/B evaluation benchmark (Raw vs Governed)");
        command.AddOption(datasetOption);
        command.AddOption(outputOption);

        command.SetHandler(async (dataset, output) =>
        {
            var runner = new EvaluationRunner();
            await runner.RunAsync(dataset, output);
        }, datasetOption, outputOption);

        return command;
    }
}
