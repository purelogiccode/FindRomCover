namespace FindRomCover;

/// <summary>Application-wide constants for file names, defaults, bug reports, themes, algorithms, messages and AI providers.</summary>
public static class AppConstants
{
    /// <summary>File name of the MAME data file.</summary>
    public const string MameDatFileName = "mame.dat";

    /// <summary>File name of the settings file.</summary>
    public const string SettingsFileName = "settings.dat";

    /// <summary>File name of the settings database file.</summary>
    public const string SettingsDatabaseFileName = "Settings.dat";

    /// <summary>File name of the query history file.</summary>
    public const string QueryHistoryFileName = "QueryHistory.dat";

    /// <summary>
    ///     String comparer for file-system paths: case-insensitive on Windows,
    ///     case-sensitive on Linux and macOS where paths are case-sensitive.
    /// </summary>
    public static StringComparer FilePathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Default memory limit in bytes (512 MB).</summary>
    public const long DefaultMemoryLimit = 512L * 1024 * 1024;

    /// <summary>Default maximum number of worker threads.</summary>
    public const int DefaultThreadLimit = 4;

    /// <summary>API key used when sending bug reports.</summary>
    public const string BugReportApiKey = "hjh7yu6t56tyr540o9u8767676r5674534453235264c75b6t7ggghgg76trf564e";

    /// <summary>Endpoint URL of the bug report service.</summary>
    public const string BugReportApiUrl = "https://www.purelogiccode.com/bugreport/api/send-bug-report";

    /// <summary>Names of the available UI theme variants.</summary>
    public static class Themes
    {
        /// <summary>Name of the light theme.</summary>
        public const string Light = "Light";

        /// <summary>Name of the dark theme.</summary>
        public const string Dark = "Dark";
    }

    /// <summary>Display names of the supported string similarity algorithms.</summary>
    public static class Algorithms
    {
        /// <summary>Display name of the Jaro-Winkler distance algorithm.</summary>
        public const string JaroWinkler = "Jaro-Winkler Distance";

        /// <summary>Display name of the Jaccard similarity algorithm.</summary>
        public const string Jaccard = "Jaccard Similarity";

        /// <summary>Display name of the Levenshtein distance algorithm.</summary>
        public const string Levenshtein = "Levenshtein Distance";
    }

    /// <summary>Default user-facing message strings.</summary>
    public static class Messages
    {
        /// <summary>Default similarity threshold value displayed to the user.</summary>
        public const string DefaultSimilarityThreshold = "70";

        /// <summary>Prefix used when listing missing covers.</summary>
        public const string MissingCoversPrefix = "MISSING COVERS: ";
    }

    /// <summary>Identifiers, endpoints and default models of the supported AI providers.</summary>
    public static class AiProviders
    {
        /// <summary>Identifier of the OpenRouter provider.</summary>
        public const string OpenRouter = "OpenRouter";

        /// <summary>Identifier of the OpenAI provider.</summary>
        public const string OpenAi = "OpenAI";

        /// <summary>Identifier of the Anthropic provider.</summary>
        public const string Anthropic = "Anthropic";

        /// <summary>Identifier of the Google Gemini provider.</summary>
        public const string Gemini = "Gemini";

        /// <summary>Identifier of the Z.ai GLM provider.</summary>
        public const string Glm = "GLM";

        /// <summary>Identifier of the local provider.</summary>
        public const string Local = "Local";

        /// <summary>Identifier of a custom OpenAI-compatible provider.</summary>
        public const string CustomOpenAi = "Custom (OpenAI-compatible)";

        /// <summary>Identifier of a custom Anthropic-compatible provider.</summary>
        public const string CustomAnthropic = "Custom (Anthropic-compatible)";

        /// <summary>Base URL of the OpenRouter API.</summary>
        public const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";

        /// <summary>Base URL of the OpenAI API.</summary>
        public const string OpenAiBaseUrl = "https://api.openai.com/v1";

        /// <summary>Base URL of the Anthropic API.</summary>
        public const string AnthropicBaseUrl = "https://api.anthropic.com/v1";

        /// <summary>Base URL of the Google Gemini API.</summary>
        public const string GeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta";

        /// <summary>Base URL of the Z.ai GLM API.</summary>
        public const string GlmBaseUrl = "https://api.z.ai/api/paas/v4";

        /// <summary>Base URL of the local OpenAI-compatible API (Ollama).</summary>
        public const string LocalBaseUrl = "http://localhost:11434/v1";

        /// <summary>Default model used with OpenRouter.</summary>
        public const string DefaultOpenRouterModel = "google/gemma-4-26b-a4b-it";

        /// <summary>Default model used with OpenAI.</summary>
        public const string DefaultOpenAiModel = "gpt-4o-mini";

        /// <summary>Default model used with Anthropic.</summary>
        public const string DefaultAnthropicModel = "claude-sonnet-4-5";

        /// <summary>Default model used with Google Gemini.</summary>
        public const string DefaultGeminiModel = "gemini-2.5-flash";

        /// <summary>Default model used with Z.ai GLM.</summary>
        public const string DefaultGlmModel = "glm-4.5v";

        /// <summary>Default model used with the local provider.</summary>
        public const string DefaultLocalModel = "qwen2.5vl:7b";

        /// <summary>Version identifier of the AI prompt template.</summary>
        public const string PromptVersion = "v2";

        /// <summary>All supported provider identifiers.</summary>
        public static readonly string[] All =
        [
            OpenRouter, OpenAi, Anthropic, Gemini, Glm, Local, CustomOpenAi, CustomAnthropic
        ];
    }
}