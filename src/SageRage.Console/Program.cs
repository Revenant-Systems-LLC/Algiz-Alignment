using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SageRage;
using SageRage.Domain;
using SageRage.Infrastructure;
using SageRage.Proxy;

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

        // Setup check
        if (args.Length > 0 && args[0].Equals("--setup", StringComparison.OrdinalIgnoreCase))
        {
            SecretLoader.RunSetup();
            return;
        }

        if (!SecretLoader.IsConfigured())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("First run detected. Running secrets setup...");
            Console.ResetColor();
            SecretLoader.RunSetup();
        }

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
        Console.WriteLine("  [X] Proxy \u2014 alignment proxy for any OpenAI-compatible API");
        Console.Write("\nMode > ");

        var mode = (Console.ReadLine()?.Trim() ?? "T").ToUpperInvariant();
        Console.WriteLine();

        if (mode == "A")
            await RunAudioMode(personaName, personaInstruction);
        else if (mode == "X")
            await RunProxyMode(personaInstruction);
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

    // ───────────────────────────────────────────────
    // PROXY MODE  (Alignment proxy server)
    // ───────────────────────────────────────────────
    private static async Task RunProxyMode(string? personaInstruction)
    {
        Console.WriteLine("\u2500\u2500 SAGE-RAGE Proxy Configuration \u2500\u2500\n");

        // Step 1: Load secrets from the DPAPI-encrypted profile
        string? apiKey = null;
        var (secretsOk, secretKeys, secretsCfg) = SecretLoader.LoadFromConfig();
        if (secretsOk)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Loaded {secretKeys.Count} keys from encrypted profile '{secretsCfg.Profile}'");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  No encrypted secrets found for profile '{secretsCfg.Profile}'.");
            Console.WriteLine("  Using environment variables only. Run with --setup to configure keys.");
            Console.ResetColor();
        }

        // Step 2: Select upstream provider
        Console.WriteLine("\nSelect upstream provider:");
        Console.WriteLine("  [O] OpenAI       (https://api.openai.com/v1)");
        Console.WriteLine("  [G] Gemini       (https://generativelanguage.googleapis.com/v1beta/openai)");
        Console.WriteLine("  [X] xAI / Grok   (https://api.x.ai/v1)");
        Console.WriteLine("  [R] OpenRouter   (https://openrouter.ai/api/v1)");
        Console.WriteLine("  [L] Local        (http://localhost:1234/v1)");
        Console.WriteLine("  [C] Custom URL");
        Console.Write("\nUpstream > ");

        var provChoice = (Console.ReadLine()?.Trim() ?? "L").ToUpperInvariant();

        string upstreamUrl;
        string? envKeyName;
        switch (provChoice)
        {
            case "O":
                upstreamUrl = "https://api.openai.com/v1";
                envKeyName = "OPENAI_API_KEY";
                break;
            case "G":
                upstreamUrl = "https://generativelanguage.googleapis.com/v1beta/openai";
                envKeyName = "GEMINI_API_KEY";
                break;
            case "X":
                upstreamUrl = "https://api.x.ai/v1";
                envKeyName = "XAI_API_KEY";
                break;
            case "R":
                upstreamUrl = "https://openrouter.ai/api/v1";
                envKeyName = "OPENROUTER_API_KEY";
                break;
            case "C":
                Console.Write("Custom base URL > ");
                upstreamUrl = Console.ReadLine()?.Trim() ?? "http://localhost:1234/v1";
                Console.Write("Env var name for API key (Enter = none) > ");
                envKeyName = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(envKeyName)) envKeyName = null;
                break;
            default:
                upstreamUrl = "http://localhost:1234/v1";
                envKeyName = null;
                break;
        }

        // Resolve the API key from environment (loaded from the encrypted secrets profile, or system)
        if (envKeyName is not null)
        {
            apiKey = SecretLoader.GetKey(envKeyName);
            if (apiKey is not null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  API key resolved from {envKeyName}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [ERROR] {envKeyName} not found in environment.");
                Console.WriteLine("  Load secrets first (unlock B drive + Load-Secrets RSPF)");
                Console.ResetColor();
                return;
            }
        }

        // Step 3: Proxy settings
        Console.Write("\nProxy port (Enter = 9443) > ");
        var portInput = Console.ReadLine()?.Trim();
        var port = int.TryParse(portInput, out var p) ? p : 9443;

        Console.WriteLine("\nPipeline options:");
        Console.WriteLine("  [F] Full    \u2014 ethics + operators + guardrails (adds latency)");
        Console.WriteLine("  [E] Ethics  \u2014 ethics checks only (fast)");
        Console.WriteLine("  [P] Pass    \u2014 pass-through with logging only");
        Console.Write("\nPipeline > ");

        var pipelineChoice = (Console.ReadLine()?.Trim() ?? "F").ToUpperInvariant();

        var enablePipeline = pipelineChoice == "F";
        var enableEthics = pipelineChoice != "P";
        var enableGuardrails = pipelineChoice == "F";

        Console.Write("Log directory (Enter = none) > ");
        var logDir = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(logDir)) logDir = null;

        var config = new ProxyConfig
        {
            UpstreamBaseUrl = upstreamUrl,
            Port = port,
            EnablePipeline = enablePipeline,
            EnableEthicsChecks = enableEthics,
            EnableGuardrails = enableGuardrails,
            LogDirectory = logDir,
            UpstreamApiKey = apiKey,
            PersonaInstruction = personaInstruction
        };

        Console.WriteLine();
        Console.WriteLine($"Upstream : {config.UpstreamBaseUrl}");
        Console.WriteLine($"API Key  : {(apiKey is not null ? "\u2705 loaded (hidden)" : "none / client-sent")}");
        Console.WriteLine($"Proxy    : {config.ListenUrl} \u2705");
        Console.WriteLine($"Pipeline : {(enablePipeline ? "Full" : enableEthics ? "Ethics only" : "Pass-through")}");
        Console.WriteLine($"Logging  : {(logDir ?? "disabled")}");
        Console.WriteLine(new string('\u2500', 50));
        Console.WriteLine();
        Console.WriteLine("Agents connect to:");
        Console.WriteLine($"  {config.ListenUrl}/v1/chat/completions");
        Console.WriteLine();
        Console.WriteLine("  No API key required \u2014 the proxy holds the real key.");
        Console.WriteLine();
        Console.WriteLine("Health check:");
        Console.WriteLine($"  {config.ListenUrl}/health");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop.\n");

        await using var server = new ProxyServer(config);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nShutting down proxy...");
        };

        await server.StartAsync(cts.Token);

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

}
