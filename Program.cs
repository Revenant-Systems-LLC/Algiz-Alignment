using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SageRage;
using SageRage.Domain;
using SageRage.Infrastructure;

internal static class Program
{
    private static readonly Dictionary<string, string> Personas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = "Delta",
            ["2"] = "Karne",
            ["3"] = "Keystone",
            ["4"] = "Noir",
            ["5"] = "V"
        };

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("SAGE-RAGE Agent \u2014 Online");
        Console.WriteLine("\u03a9([\u21a6(\u039e, \u2205)]) \u2192 \u03c7");
        Console.WriteLine(new string('\u2500', 30));

        // ── Persona Selection ────────────────────────────────
        Console.WriteLine("Select Agent Persona:");
        Console.WriteLine("  [1] Delta");
        Console.WriteLine("  [2] Karne");
        Console.WriteLine("  [3] Keystone");
        Console.WriteLine("  [4] Noir");
        Console.WriteLine("  [5] V");
        Console.Write("\nChoice > ");

        var choice = Console.ReadLine()?.Trim() ?? "1";
        if (!Personas.TryGetValue(choice, out var personaName))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Invalid \u2014 defaulting to Delta.");
            Console.ResetColor();
            personaName = "Delta";
        }

        var baseDir     = AppContext.BaseDirectory;
        var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        var personaPath = Path.Combine(projectRoot, "prompt", "persona", $"{personaName}.md");
        if (!File.Exists(personaPath))
            personaPath = Path.Combine("prompt", "persona", $"{personaName}.md");

        string? personaInstruction = null;
        try
        {
            personaInstruction = File.ReadAllText(personaPath, System.Text.Encoding.UTF8);
            Console.WriteLine($"Persona  : {personaName} \u2705");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARN] Could not load '{personaPath}': {ex.Message}");
            Console.ResetColor();
        }

        // ── Mode Selection ────────────────────────────────
        Console.WriteLine("\nSelect Mode:");
        Console.WriteLine("  [T] Text  \u2014 type messages, read responses");
        Console.WriteLine("  [A] Audio \u2014 mic in / audio out  (Gemini Live API)");
        Console.Write("\nMode > ");

        var mode = (Console.ReadLine()?.Trim() ?? "T").ToUpperInvariant();
        Console.WriteLine();

        if (mode == "A")
            await RunAudioMode(personaName, personaInstruction);
        else
            await RunTextMode(personaName, personaInstruction);

        Console.WriteLine("\nSAGE-RAGE: Shutdown complete.");
    }

    // ───────────────────────────────────────────────
    // TEXT MODE
    // ───────────────────────────────────────────────
    private static async Task RunTextMode(string personaName, string? personaInstruction)
    {
        Console.WriteLine("Select Provider:");
        Console.WriteLine($"  [G] Gemini Cloud     (REST \u2014 {GeminiLLMProvider.PrimaryModel})");
        Console.WriteLine($"  [C] Claude/Anthropic (REST — {AnthropicProvider.PrimaryModel})");
        Console.WriteLine($"  [P] OpenAI           (REST — {OpenAIProvider.PrimaryModel})");
        Console.WriteLine("  [L] LM Studio / GGUF (OpenAI-compatible, http://localhost:1234/v1)");
        Console.WriteLine("  [O] Ollama           (native API, http://localhost:11434)");
        Console.Write("\nProvider > ");

        var prov = (Console.ReadLine()?.Trim() ?? "G").ToUpperInvariant();
        Console.WriteLine();

        ILLMProvider llm;
        string       providerLabel;

        try
        {
            switch (prov)
            {
                case "C":
                {
                    Console.Write($"Model (Enter = {AnthropicProvider.PrimaryModel}) > ");
                    var modelIn = Console.ReadLine()?.Trim();
                    var model   = string.IsNullOrWhiteSpace(modelIn) ? null : modelIn;
                    llm           = new AnthropicProvider(model: model);
                    providerLabel = $"Anthropic ✅  [{(model ?? AnthropicProvider.PrimaryModel)}]";
                    break;
                }

                case "P":
                {
                    Console.Write($"Model (Enter = {OpenAIProvider.PrimaryModel}) > ");
                    var modelIn = Console.ReadLine()?.Trim();
                    var model   = string.IsNullOrWhiteSpace(modelIn) ? null : modelIn;
                    llm           = new OpenAIProvider(model: model);
                    providerLabel = $"OpenAI ✅  [{(model ?? OpenAIProvider.PrimaryModel)}]";
                    break;
                }

                case "L":
                {
                    Console.Write("Base URL  (Enter = http://localhost:1234/v1) > ");
                    var urlIn   = Console.ReadLine()?.Trim();
                    var baseUrl = string.IsNullOrWhiteSpace(urlIn) ? "http://localhost:1234/v1" : urlIn;

                    Console.WriteLine("\nKnown models on A:\\Models:");
                    Console.WriteLine("  Dirty-Muse-Writer-v01-Uncensored-Erotica-NSFW.i1-Q3_K_S");
                    Console.WriteLine("  gemma-2-9b-it-abliterated-Q5_K_M");
                    Console.WriteLine("  mythomax-l2-13b.Q4_K_M");
                    Console.Write("\nModel name (as shown in LM Studio) > ");

                    var modelName = Console.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(modelName))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[ERROR] Model name required.");
                        Console.ResetColor();
                        return;
                    }

                    llm           = new OpenAICompatibleProvider(baseUrl, modelName);
                    providerLabel = $"LM Studio \u2705  [{baseUrl}]  model: {modelName}";
                    break;
                }

                case "O":
                {
                    Console.Write("Base URL  (Enter = http://localhost:11434) > ");
                    var urlIn   = Console.ReadLine()?.Trim();
                    var baseUrl = string.IsNullOrWhiteSpace(urlIn) ? "http://localhost:11434" : urlIn;

                    Console.Write("Model tag (e.g. llama3, gemma2) > ");
                    var modelTag = Console.ReadLine()?.Trim() ?? "llama3";

                    llm           = new OllamaProvider(modelTag, baseUrl);
                    providerLabel = $"Ollama \u2705  [{baseUrl}]  model: {modelTag}";
                    break;
                }

                default:
                {
                    Console.Write($"Model (Enter = {GeminiLLMProvider.PrimaryModel}) > ");
                    var modelIn = Console.ReadLine()?.Trim();
                    var model   = string.IsNullOrWhiteSpace(modelIn) ? null : modelIn;
                    llm           = new GeminiLLMProvider(model: model);
                    providerLabel = $"Gemini REST \u2705  [{(model ?? GeminiLLMProvider.PrimaryModel)}]";
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[BOOT FAILED] {ex.Message}");
            Console.ResetColor();
            return;
        }

        var agent = new SageAgent(llm, personaInstruction);
        Console.WriteLine($"Provider : {providerLabel}");
        Console.WriteLine($"Agent    : {personaName} \u2705");
        Console.WriteLine(new string('\u2500', 30));
        Console.WriteLine("Type 'exit' to quit.\n");

        while (true)
        {
            Console.Write("\nProgenitor > ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            try
            {
                var response = await agent.Process(UserMessage.From(input));
                Console.WriteLine($"\n{personaName} [{response.EmotionalState}]:");
                Console.WriteLine(response.Text);
                if (response.Metadata is not null)
                {
                    dynamic m = response.Metadata;
                    Console.WriteLine(
                        $"\n\u2500\u2500 QC  Profile:{m.QualityProfile}  Passed:{m.QualityPassed}");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {ex.GetType().Name}: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    // ───────────────────────────────────────────────
    // AUDIO MODE  (Gemini Live WebSocket)
    // ───────────────────────────────────────────────
    private static async Task RunAudioMode(string personaName, string? personaInstruction)
    {
        var devices = AudioCapture.ListDevices();
        int micDeviceIndex = 0;

        if (devices.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] No audio input devices found.");
            Console.ResetColor();
            return;
        }

        if (devices.Count == 1)
        {
            Console.WriteLine($"Mic      : {devices[0].Name} \u2705");
        }
        else
        {
            Console.WriteLine("Select Microphone:");
            foreach (var (idx, name) in devices)
                Console.WriteLine($"  [{idx}] {name}");
            Console.Write("\nMic # > ");

            if (int.TryParse(Console.ReadLine()?.Trim(), out var picked) &&
                picked >= 0 && picked < devices.Count)
                micDeviceIndex = picked;
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Invalid \u2014 using device 0 ({devices[0].Name}).");
                Console.ResetColor();
            }

            Console.WriteLine($"Mic      : {devices[micDeviceIndex].Name} \u2705");
        }

        var provider = new GeminiLiveAudioProvider();
        var config   = new LiveConfig(
            SystemInstruction:  personaInstruction ?? "You are a helpful assistant.",
            ResponseModalities: new[] { "AUDIO" });

        Console.WriteLine("Connecting to Gemini Live API...");

        ILiveAudioSession session;
        try
        {
            session = await provider.ConnectAsync(
                GeminiLiveAudioProvider.LiveModel, config);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[BOOT FAILED] Live API: {ex.Message}");
            Console.ResetColor();
            return;
        }

        await using var _ = session;

        using var cts      = new CancellationTokenSource();
        using var playback = new AudioPlayback();

        var ready = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var receiveTask = Task.Run(async () =>
        {
            await foreach (var evt in session.ReceiveAsync(cts.Token))
            {
                switch (evt)
                {
                    case SetupComplete:
                        ready.TrySetResult();
                        break;
                    case OutputAudioChunk audio:
                        playback.QueueAudio(audio.Data);
                        break;
                    case OutputTranscription { Text: var text }
                        when !string.IsNullOrWhiteSpace(text):
                        Console.WriteLine($"\n{personaName}: {text}");
                        break;
                    case InputTranscription { Text: var heard }
                        when !string.IsNullOrWhiteSpace(heard):
                        Console.WriteLine($"\n[You]: {heard}");
                        break;
                    case TurnComplete:
                        Console.Write("\nProgenitor > ");
                        break;
                }
            }
        });

        await ready.Task;
        Console.WriteLine($"Live API : Connected \u2705  ({GeminiLiveAudioProvider.LiveModel})");
        Console.WriteLine($"Agent    : {personaName} \u2705");
        Console.WriteLine(new string('\u2500', 30));
        Console.WriteLine("Commands: 'mic' = toggle microphone | 'exit' = quit\n");
        Console.Write("Progenitor > ");

        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        AudioCapture? capture   = null;
        Task?         micTask   = null;
        var           micActive = false;

        while (!cts.IsCancellationRequested)
        {
            var input = Console.ReadLine();
            if (input is null || cts.IsCancellationRequested) break;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            if (input.Equals("mic", StringComparison.OrdinalIgnoreCase))
            {
                if (!micActive)
                {
                    capture   = new AudioCapture(deviceNumber: micDeviceIndex);
                    micActive = true;
                    capture.Start();
                    Console.WriteLine("\uD83C\uDF99 Microphone ON \u2014 speak now. Type 'mic' to stop.");
                    micTask = Task.Run(async () =>
                    {
                        try
                        {
                            await foreach (var chunk in capture.ReadAsync(cts.Token))
                                await session.SendAudioAsync(chunk, cts.Token);
                        }
                        catch (OperationCanceledException) { }
                    });
                }
                else
                {
                    capture?.Stop();
                    capture?.Dispose();
                    capture   = null;
                    micActive = false;
                    Console.WriteLine("\uD83C\uDF99 Microphone OFF");
                    Console.Write("\nProgenitor > ");
                }
                continue;
            }

            if (!string.IsNullOrWhiteSpace(input))
            {
                try   { await session.SendTextAsync(input, endOfTurn: true, cts.Token); }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        cts.Cancel();
        capture?.Stop();
        capture?.Dispose();
        if (micTask   is not null) await micTask.ContinueWith(_ => { });
        await receiveTask.ContinueWith(_ => { });
    }
}
