namespace FindRomCover;

public static class AppConstants
{
    public const string MameDatFileName = "mame.dat";
    public const string SettingsFileName = "settings.dat";

    public const long DefaultMemoryLimit = 512L * 1024 * 1024;
    public const int DefaultThreadLimit = 4;

    public const string BugReportApiKey = "hjh7yu6t56tyr540o9u8767676r5674534453235264c75b6t7ggghgg76trf564e";
    public const string BugReportApiUrl = "https://www.purelogiccode.com/bugreport/api/send-bug-report";

    public static class Themes
    {
        public const string Light = "Light";
        public const string Dark = "Dark";
    }

    public static class Algorithms
    {
        public const string JaroWinkler = "Jaro-Winkler Distance";
        public const string Jaccard = "Jaccard Similarity";
        public const string Levenshtein = "Levenshtein Distance";
    }

    public static class Messages
    {
        public const string DefaultSimilarityThreshold = "70";
        public const string MissingCoversPrefix = "MISSING COVERS: ";
    }

    public static class AiProviders
    {
        public const string OpenRouter = "OpenRouter";
        public const string OpenAi = "OpenAI";
        public const string Anthropic = "Anthropic";
        public const string Gemini = "Gemini";
        public const string Glm = "GLM";
        public const string Local = "Local";
        public const string CustomOpenAi = "Custom (OpenAI-compatible)";
        public const string CustomAnthropic = "Custom (Anthropic-compatible)";

        public const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
        public const string OpenAiBaseUrl = "https://api.openai.com/v1";
        public const string AnthropicBaseUrl = "https://api.anthropic.com/v1";
        public const string GeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta";
        public const string GlmBaseUrl = "https://api.z.ai/api/paas/v4";
        public const string LocalBaseUrl = "http://localhost:11434/v1";

        public const string DefaultOpenRouterModel = "google/gemini-2.5-flash";
        public const string DefaultOpenAiModel = "gpt-4o-mini";
        public const string DefaultAnthropicModel = "claude-sonnet-4-5";
        public const string DefaultGeminiModel = "gemini-2.5-flash";
        public const string DefaultGlmModel = "glm-4.5v";
        public const string DefaultLocalModel = "qwen2.5vl:7b";

        public const string PromptVersion = "v1";

        public static readonly string[] All =
        [
            OpenRouter, OpenAi, Anthropic, Gemini, Glm, Local, CustomOpenAi, CustomAnthropic
        ];
    }
}