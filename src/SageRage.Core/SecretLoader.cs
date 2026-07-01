using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SageRage.Infrastructure
{
    /// <summary>
    /// Loads API keys from a DPAPI-encrypted secrets file, tied to the current
    /// Windows user account. Keys are loaded into process-scoped environment
    /// variables only — never persisted to system/user env vars, and never
    /// written to disk in plaintext.
    ///
    /// Windows-only: DPAPI (<see cref="ProtectedData"/>) has no equivalent on
    /// Linux/macOS and throws <see cref="PlatformNotSupportedException"/> there.
    /// This matches the rest of this project's Windows-targeted surfaces
    /// (SageRage.UI, SageRage.Console).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class SecretLoader
    {
        public const string DefaultProfile = "SageRage";

        private static readonly string ConfigDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SageRage");
        private static readonly string ConfigFile =
            Path.Combine(ConfigDir, "secrets.json");
        private static readonly string SecretsDir =
            Path.Combine(ConfigDir, "secrets");

        // Scopes the encrypted blob to this app specifically (DPAPI's optional
        // entropy parameter), so a file dropped in place by something else
        // can't silently be decrypted here.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SageRage.SecretLoader.v1");

        /// <summary>Persisted user configuration for secrets loading.</summary>
        public sealed class SecretsConfig
        {
            public string Profile { get; set; } = DefaultProfile;
        }

        private static string SecretsFilePath(string profile)
            => Path.Combine(SecretsDir, $"{profile}.dpapi");

        /// <summary>
        /// Load a named profile, decrypting via DPAPI and setting each key as a
        /// process-scoped environment variable. Returns the variable names loaded.
        /// </summary>
        public static IReadOnlyList<string> Load(string profile)
        {
            var path = SecretsFilePath(profile);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Secrets profile not found: {path}. Run with --setup to create one.", path);

            var values = ReadEncrypted(path);
            foreach (var (key, value) in values)
                Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);

            return new List<string>(values.Keys);
        }

        /// <summary>
        /// Try to load a profile. Returns false if no secrets file exists for it yet.
        /// </summary>
        public static bool TryLoad(string profile, out IReadOnlyList<string> loaded)
        {
            try
            {
                loaded = Load(profile);
                return true;
            }
            catch (FileNotFoundException)
            {
                loaded = Array.Empty<string>();
                return false;
            }
        }

        /// <summary>Clear all variables that were loaded from a profile.</summary>
        public static void Unload(IReadOnlyList<string> keys)
        {
            foreach (var key in keys)
                Environment.SetEnvironmentVariable(key, null, EnvironmentVariableTarget.Process);
        }

        /// <summary>
        /// Get a specific key from the environment, returning null if not set.
        /// Convenience wrapper — does NOT log or expose the value.
        /// </summary>
        public static string? GetKey(string name) => Environment.GetEnvironmentVariable(name);

        // ── Config persistence ──────────────────────────────────

        /// <summary>Check whether first-run setup has been completed.</summary>
        public static bool IsConfigured() => File.Exists(ConfigFile);

        /// <summary>Load saved configuration, or return defaults if not configured.</summary>
        public static SecretsConfig LoadConfig()
        {
            if (!File.Exists(ConfigFile))
                return new SecretsConfig();

            try
            {
                var json = File.ReadAllText(ConfigFile);
                return JsonSerializer.Deserialize<SecretsConfig>(json) ?? new SecretsConfig();
            }
            catch (JsonException)
            {
                return new SecretsConfig();
            }
        }

        /// <summary>Save configuration to disk (profile name only — never secrets).</summary>
        public static void SaveConfig(SecretsConfig config)
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
        }

        /// <summary>
        /// Encrypt and store a full set of secrets for a profile via DPAPI,
        /// scoped to the current Windows user. Overwrites any existing file.
        /// </summary>
        public static void SaveSecrets(string profile, IReadOnlyDictionary<string, string> values)
        {
            Directory.CreateDirectory(SecretsDir);
            var json = JsonSerializer.Serialize(values);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(SecretsFilePath(profile), encrypted);
        }

        private static Dictionary<string, string> ReadEncrypted(string path)
        {
            var encrypted = File.ReadAllBytes(path);
            var plainBytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Load secrets using saved config. Combines config lookup + loading.
        /// Returns (success, loaded key names, config used).
        /// </summary>
        public static (bool success, IReadOnlyList<string> loaded, SecretsConfig config) LoadFromConfig()
        {
            var config = LoadConfig();
            if (TryLoad(config.Profile, out var loaded))
                return (true, loaded, config);
            return (false, loaded, config);
        }

        // ── Setup wizard ──────────────────────────────────

        /// <summary>
        /// Well-known key names prompted for during setup. Anything else can
        /// still be loaded via <see cref="GetKey"/> once saved through
        /// <see cref="SaveSecrets"/> directly.
        /// </summary>
        private static readonly string[] KnownKeys =
        {
            "GEMINI_API_KEY", "OPENAI_API_KEY", "ANTHROPIC_API_KEY", "XAI_API_KEY",
            "OPENROUTER_API_KEY", "GIT_API_KEY", "PERPLEXITY_API_KEY", "BRAVE_API_KEY",
            "HFACE_API_KEY", "NVI_API_KEY", "NEWS_API_KEY"
        };

        /// <summary>
        /// Interactive first-run setup. Prompts for known API keys and encrypts
        /// them to disk via DPAPI, tied to the current Windows user account.
        /// Existing values are preserved for any key left blank. Returns the
        /// resolved config.
        /// </summary>
        public static SecretsConfig RunSetup()
        {
            Console.WriteLine("\n── SAGE-RAGE Secrets Setup ──\n");
            Console.WriteLine("Keys are encrypted with DPAPI, tied to your Windows user account,");
            Console.WriteLine("and are never written to disk in plaintext.\n");

            Console.Write($"Profile name (Enter = {DefaultProfile}) > ");
            var profileInput = Console.ReadLine()?.Trim();
            var profile = string.IsNullOrWhiteSpace(profileInput) ? DefaultProfile : profileInput;

            var existing = TryLoad(profile, out _)
                ? new Dictionary<string, string>(ReadEncrypted(SecretsFilePath(profile)))
                : new Dictionary<string, string>();

            foreach (var key in KnownKeys)
            {
                var hasExisting = existing.TryGetValue(key, out var currentValue) && !string.IsNullOrEmpty(currentValue);
                Console.Write($"{key}{(hasExisting ? " (Enter = keep existing)" : "")} > ");
                var input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    existing[key] = input.Trim();
            }

            SaveSecrets(profile, existing);

            var config = new SecretsConfig { Profile = profile };
            SaveConfig(config);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  Saved and encrypted to {SecretsFilePath(profile)}");
            Console.WriteLine("  Run setup again with --setup to add or change keys.\n");
            Console.ResetColor();

            return config;
        }
    }
}
