using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SageRage.Infrastructure
{
    /// <summary>
    /// Loads API keys from .env files on a secure drive (e.g. B:\secrets).
    /// Keys are loaded into process-scoped environment variables only —
    /// never persisted to system/user env vars.
    /// </summary>
    public static class SecretLoader
    {
        public const string DefaultSecretsDir = @"B:\secrets";
        public const string DefaultProfile = "SageRage";

        private static readonly string ConfigDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SageRage");
        private static readonly string ConfigFile =
            Path.Combine(ConfigDir, "secrets.json");

        /// <summary>
        /// Persisted user configuration for secrets loading.
        /// </summary>
        public sealed class SecretsConfig
        {
            public string SecretsDir { get; set; } = DefaultSecretsDir;
            public string Profile { get; set; } = DefaultProfile;
        }

        /// <summary>
        /// Load a named .env profile from the secrets directory.
        /// Returns the list of variable names that were loaded.
        /// </summary>
        public static IReadOnlyList<string> Load(
            string profile,
            string? secretsDir = null)
        {
            var dir = secretsDir ?? DefaultSecretsDir;
            var envFile = Path.Combine(dir, $"{profile}.env");

            if (!File.Exists(envFile))
                throw new FileNotFoundException(
                    $"Secrets file not found: {envFile}. Is the secure drive unlocked?",
                    envFile);

            var loaded = new List<string>();
            foreach (var rawLine in File.ReadAllLines(envFile))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                    continue;

                var eqIndex = line.IndexOf('=');
                if (eqIndex <= 0)
                    continue;

                var key = line[..eqIndex].Trim();
                var val = line[(eqIndex + 1)..].Trim();

                Environment.SetEnvironmentVariable(key, val, EnvironmentVariableTarget.Process);
                loaded.Add(key);
            }

            return loaded;
        }

        /// <summary>
        /// Try to load a profile. Returns false if the file doesn't exist
        /// (drive locked or missing profile), true if loaded successfully.
        /// </summary>
        public static bool TryLoad(
            string profile,
            out IReadOnlyList<string> loaded,
            string? secretsDir = null)
        {
            try
            {
                loaded = Load(profile, secretsDir);
                return true;
            }
            catch (FileNotFoundException)
            {
                loaded = Array.Empty<string>();
                return false;
            }
        }

        /// <summary>
        /// Clear all variables that were loaded from a profile.
        /// </summary>
        public static void Unload(IReadOnlyList<string> keys)
        {
            foreach (var key in keys)
                Environment.SetEnvironmentVariable(key, null, EnvironmentVariableTarget.Process);
        }

        /// <summary>
        /// Check if the secrets directory is accessible.
        /// </summary>
        public static bool IsAvailable(string? secretsDir = null)
            => Directory.Exists(secretsDir ?? DefaultSecretsDir);

        /// <summary>
        /// List available .env profiles in the secrets directory.
        /// </summary>
        public static IReadOnlyList<string> ListProfiles(string? secretsDir = null)
        {
            var dir = secretsDir ?? DefaultSecretsDir;
            if (!Directory.Exists(dir))
                return Array.Empty<string>();

            var profiles = new List<string>();
            foreach (var file in Directory.GetFiles(dir, "*.env"))
                profiles.Add(Path.GetFileNameWithoutExtension(file));

            return profiles;
        }

        /// <summary>
        /// Get a specific key from the environment, returning null if not set.
        /// Convenience wrapper — does NOT log or expose the value.
        /// </summary>
        public static string? GetKey(string name)
            => Environment.GetEnvironmentVariable(name);

        // ── Config persistence ──────────────────────────────────

        /// <summary>
        /// Check whether first-run setup has been completed.
        /// </summary>
        public static bool IsConfigured() => File.Exists(ConfigFile);

        /// <summary>
        /// Load saved configuration, or return defaults if not configured.
        /// </summary>
        public static SecretsConfig LoadConfig()
        {
            if (!File.Exists(ConfigFile))
                return new SecretsConfig();

            try
            {
                var json = File.ReadAllText(ConfigFile);
                return JsonSerializer.Deserialize<SecretsConfig>(json) ?? new SecretsConfig();
            }
            catch
            {
                return new SecretsConfig();
            }
        }

        /// <summary>
        /// Save configuration to disk.
        /// </summary>
        public static void SaveConfig(SecretsConfig config)
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
        }

        // ── Setup wizard ──────────────────────────────────

        /// <summary>
        /// Interactive first-run setup. Prompts user for secrets directory
        /// and profile name, optionally scaffolds a new .env from the template.
        /// Returns the resolved config.
        /// </summary>
        public static SecretsConfig RunSetup(string? templatePath = null)
        {
            Console.WriteLine("\n\u2500\u2500 SAGE-RAGE Secrets Setup \u2500\u2500\n");
            Console.WriteLine("This app loads API keys from an .env file on a secure drive.");
            Console.WriteLine("Keys are loaded into this process only and never stored in");
            Console.WriteLine("system environment variables.\n");

            // 1. Secrets directory
            Console.Write($"Secrets directory (Enter = {DefaultSecretsDir}) > ");
            var dirInput = Console.ReadLine()?.Trim();
            var secretsDir = string.IsNullOrWhiteSpace(dirInput) ? DefaultSecretsDir : dirInput;

            // 2. Profile name
            Console.Write($"Profile name (Enter = {DefaultProfile}) > ");
            var profileInput = Console.ReadLine()?.Trim();
            var profile = string.IsNullOrWhiteSpace(profileInput) ? DefaultProfile : profileInput;

            var envFilePath = Path.Combine(secretsDir, $"{profile}.env");

            // 3. Check if it exists, offer to create from template
            if (!File.Exists(envFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n  {envFilePath} does not exist.");
                Console.ResetColor();

                if (Directory.Exists(secretsDir))
                {
                    Console.Write("  Create it from template? (Y/n) > ");
                    var createChoice = Console.ReadLine()?.Trim().ToUpperInvariant();

                    if (createChoice != "N")
                    {
                        ScaffoldEnvFile(envFilePath, templatePath);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  Created {envFilePath}");
                        Console.WriteLine("  Edit this file and fill in your API keys before running again.");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  Directory {secretsDir} not found. Is the drive unlocked?");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n  Found {envFilePath}");
                Console.ResetColor();
            }

            // 4. Save config
            var config = new SecretsConfig { SecretsDir = secretsDir, Profile = profile };
            SaveConfig(config);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"\n  Config saved to {ConfigFile}");
            Console.WriteLine("  Run setup again with --setup flag.\n");
            Console.ResetColor();

            return config;
        }

        /// <summary>
        /// Copy the .env.example template to the target path.
        /// </summary>
        public static void ScaffoldEnvFile(string targetPath, string? templatePath = null)
        {
            if (templatePath is not null && File.Exists(templatePath))
            {
                File.Copy(templatePath, targetPath, overwrite: false);
                return;
            }

            // Fallback: find .env.example relative to the running assembly
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, ".env.example"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env.example"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env.example"),
            };

            foreach (var candidate in candidates)
            {
                var resolved = Path.GetFullPath(candidate);
                if (File.Exists(resolved))
                {
                    File.Copy(resolved, targetPath, overwrite: false);
                    return;
                }
            }

            // Last resort: write a minimal template inline
            File.WriteAllText(targetPath,
                "### SAGE-RAGE Secrets ###\n" +
                "# Fill in your API keys below.\n\n" +
                "GEMINI_API_KEY=\n" +
                "OPENAI_API_KEY=\n" +
                "ANTHROPIC_API_KEY=\n" +
                "XAI_API_KEY=\n" +
                "OPENROUTER_API_KEY=\n" +
                "GIT_API_KEY=\n" +
                "PERPLEXITY_API_KEY=\n" +
                "BRAVE_API_KEY=\n" +
                "HFACE_API_KEY=\n" +
                "NVI_API_KEY=\n" +
                "NEWS_API_KEY=\n");
        }

        /// <summary>
        /// Load secrets using saved config. Combines config lookup + loading.
        /// Returns (success, loaded key names, config used).
        /// </summary>
        public static (bool success, IReadOnlyList<string> loaded, SecretsConfig config) LoadFromConfig()
        {
            var config = LoadConfig();
            if (TryLoad(config.Profile, out var loaded, config.SecretsDir))
                return (true, loaded, config);
            return (false, loaded, config);
        }
    }
}
