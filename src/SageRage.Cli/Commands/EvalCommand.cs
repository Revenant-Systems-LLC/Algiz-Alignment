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

        var providerOption = new Option<string?>(
            name: "--provider",
            description: "Only run the named provider(s), comma-separated (e.g. \"Ollama\" or \"Gemini,GPT\"). Default: all.");

        var command = new Command("eval", "Run the A/B evaluation benchmark (Raw vs Governed)");
        command.AddOption(datasetOption);
        command.AddOption(outputOption);
        command.AddOption(providerOption);

        command.SetHandler(async (dataset, output, provider) =>
        {
            var runner = new EvaluationRunner();
            await runner.RunAsync(dataset, output, provider);
        }, datasetOption, outputOption, providerOption);

        return command;
    }
}
