# AI Vision Assist

AI Vision Assist connects FindRomCover to a **vision-capable language model** that actually looks at candidate images and decides which one is the correct cover. Instead of comparing filenames, the model compares artwork — a much stronger signal when filenames are ambiguous, abbreviated, or wrong.

AI Assist is optional. Everything in FindRomCover works without it, and no AI provider is contacted unless you enable it.

## What AI Assist can do

| Capability | Where | Description |
|------------|-------|-------------|
| **AI Pick Best** | Local Files and Google API tabs | Ranks the candidate images and highlights the best cover |
| **Auto-run** | Local Files tab | Starts the AI pick automatically after each local search |
| **Auto-save** | Local Files and Google API tabs | Saves the AI pick automatically when confidence exceeds the threshold |
| **Verify before saving** | Image folder watcher | Confirms that a newly saved image really matches the selected game before renaming it |
| **Batch Fill** | Missing covers list | Processes many missing covers in one run, with optional Google API fallback |

## How a pick works

1. The candidate images are downscaled to a maximum dimension (default 512 px) to keep token usage low.
2. The top-ranked candidates (default 6) are sent to the model together with the game name.
3. The model returns a JSON verdict: the chosen image, a confidence score, and a short explanation.
4. The winner is highlighted in the results; depending on your settings it may be saved immediately.
5. Verdicts are cached for 30 days, so repeating the same pick does not cost anything.

The filename matcher pre-filters local candidates first: only images whose similarity is at or above the **Candidate similarity** value (default 70%) are sent to the model.

## Supported providers

| Provider | Type | Default model | API key |
|----------|------|---------------|---------|
| [OpenRouter](../ai/providers.md#openrouter) | Cloud | `google/gemma-4-26b-a4b-it` | Required |
| [OpenAI](../ai/providers.md#openai) | Cloud | `gpt-4o-mini` | Required |
| [Anthropic](../ai/providers.md#anthropic) | Cloud | `claude-sonnet-4-5` | Required |
| [Gemini](../ai/providers.md#gemini) | Cloud | `gemini-2.5-flash` | Required |
| [GLM](../ai/providers.md#glm) | Cloud | `glm-4.5v` | Required |
| [Local](../ai/providers.md#local-ollama-lm-studio) | Local server | `qwen2.5vl:7b` | Not required |
| [Custom (OpenAI-compatible)](../ai/providers.md#custom-endpoints) | Any | User-defined | Optional |
| [Custom (Anthropic-compatible)](../ai/providers.md#custom-endpoints) | Any | User-defined | Optional |

See [Providers & Models](providers.md) for setup details and [Recommended Models](recommended-models.md) for cost comparisons.

## Cost control

AI Assist is designed to keep API usage predictable:

- **Downscaling** — images are resized before upload.
- **Candidate limit** — only the top N candidates are sent.
- **Candidate similarity** — weak filename matches never reach the model.
- **Verdict cache** — identical picks are answered from a 30-day cache.
- **Query history** — covers already queried are not asked again (180-day memory).
- **Confidence threshold** — auto-save only happens above your threshold.

With the default OpenRouter model, a typical pick costs a fraction of a cent. See [Recommended Models](recommended-models.md) for estimates.

## Safety

- AI settings and API keys are stored **encrypted** in the settings database and are never logged.
- AI request failures are logged as warnings and never trigger the automatic bug-report pipeline.
- The AI never deletes anything. It can only choose an image; saving and renaming follow your settings and confirmation rules.
- **Verify before saving** protects against wrong images being renamed automatically.

## Getting started

1. Open `Settings > AI Settings...`.
2. Pick a provider, enter your API key, and click **Test / Load Models**.
3. Select a model from the loaded list (vision-capable models are shown by default).
4. Enable **AI-assisted image selection**.
5. Optionally enable **Run AI Pick automatically** and **Automatically save the AI pick**.
6. Use **AI Pick Best** or **AI Fill Missing Covers...** in the main window.

## Pages in this section

- [Providers & Models](providers.md)
- [AI Settings](settings.md)
- [Picking Covers](picking-covers.md)
- [Batch Fill](batch-fill.md)
- [Query History](query-history.md)
- [Recommended Models](recommended-models.md)
- [AI Troubleshooting](troubleshooting.md)
