# Providers & Models

FindRomCover supports cloud providers, local servers, and custom endpoints. All of them are configured in `Settings > AI Settings...`. **Test / Load Models** discovers a provider's models at `<Base URL>/models`, so the endpoint must expose that path.

## Provider comparison

| Provider | Default base URL | Default model | Key required | Notes |
|----------|------------------|---------------|--------------|-------|
| OpenRouter | `https://openrouter.ai/api/v1` | `google/gemma-4-26b-a4b-it` | Yes | Aggregator with hundreds of models; cheapest vision options |
| OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` | Yes | Reliable JSON output; `gpt-5-nano` is a low-cost alternative |
| Anthropic | `https://api.anthropic.com/v1` | `claude-sonnet-4-5` | Yes | Strong visual reasoning; native Messages API |
| Gemini | `https://generativelanguage.googleapis.com/v1beta` | `gemini-2.5-flash` | Yes | Generous free tier; `gemini-2.5-flash-lite` is cheaper |
| GLM | `https://api.z.ai/api/paas/v4` | `glm-4.5v` | Yes | Zhipu/Z.AI; use `https://open.bigmodel.cn/api/paas/v4` for the China endpoint |
| Local | `http://localhost:11434/v1` | `qwen2.5vl:7b` | No | Ollama or LM Studio; no cloud cost, slower on weak hardware |
| Custom (OpenAI-compatible) | None — enter manually | None — enter manually | Optional | Proxies, gateways, self-hosted servers |
| Custom (Anthropic-compatible) | Falls back to `https://api.anthropic.com/v1` when left empty | None — enter manually | Optional | Endpoints speaking the Anthropic Messages API |

## OpenRouter

1. Create an API key at [openrouter.ai/keys](https://openrouter.ai/keys).
2. Select **OpenRouter** in AI Settings, paste the key, and click **Test / Load Models**.
3. Pick a model. The default `google/gemma-4-26b-a4b-it` follows the cover/box-art instructions reliably; see [Recommended Models](recommended-models.md) for cheaper and more expensive alternatives.

OpenRouter exposes modality metadata, so the model picker knows exactly which models accept image input.

## OpenAI

1. Create an API key at [platform.openai.com/api-keys](https://platform.openai.com/api-keys).
2. Select **OpenAI**, paste the key, and click **Test / Load Models**.
3. Choose a vision-capable model such as `gpt-4o-mini` or `gpt-5-nano`.

The OpenAI adapter uses the shared OpenAI-compatible client. Reasoning models (`o1`, `o3`, `o4`, `gpt-5*`) are called without `temperature` and with `max_completion_tokens`; other models use `temperature` and `max_tokens`. All requests allow up to 4,096 output tokens by default, and JSON is parsed from the response text.

## Anthropic

1. Create an API key at [console.anthropic.com](https://console.anthropic.com).
2. Select **Anthropic**, paste the key, and click **Test / Load Models**.
3. Choose a model such as `claude-sonnet-4-5` or `claude-haiku-4-5` for lower cost.

Anthropic uses a native adapter implementing the Messages API, including image blocks.

## Gemini

1. Create an API key at [aistudio.google.com/apikey](https://aistudio.google.com/apikey).
2. Select **Gemini**, paste the key, and click **Test / Load Models**.
3. Choose a model such as `gemini-2.5-flash` or `gemini-2.5-flash-lite`.

Gemini uses a native adapter implementing `generateContent` with inline image data. The free tier is generous, which makes it a good starting point.

## GLM

1. Create an API key at [z.ai](https://z.ai).
2. Select **GLM**, paste the key, and click **Test / Load Models**.
3. Choose `glm-4.5v` or another vision-capable GLM model.

For the China endpoint, change the Base URL to `https://open.bigmodel.cn/api/paas/v4`.

## Local (Ollama / LM Studio)

Run a local vision model and point FindRomCover at it. No API key is required.

**Ollama**

```bash
ollama pull qwen2.5vl:7b
ollama serve
```

- Base URL: `http://localhost:11434/v1`
- Model: `qwen2.5vl:7b` (or another vision model you have pulled)

**LM Studio**

- Start the local server in LM Studio (default port 1234).
- Base URL: `http://localhost:1234/v1`
- Model: the identifier shown by LM Studio.

Local models are private and free to run, but they are slower and generally less accurate than cloud models. They are a good fit for small collections or offline setups.

## Custom endpoints

Use a custom provider when you have a proxy, gateway, or self-hosted server:

| Option | API style | Base URL | When to use |
|--------|-----------|----------|-------------|
| Custom (OpenAI-compatible) | `/chat/completions` | Required — no default | Anything that speaks the OpenAI API, including Azure OpenAI gateways and LiteLLM |
| Custom (Anthropic-compatible) | `/messages` | Optional — defaults to `https://api.anthropic.com/v1` | Endpoints that speak the Anthropic Messages API |

Enter the API key, Base URL, and Model manually. The Base URL and Model are required for custom providers; the API key is optional at save time because some self-hosted servers ignore it, but gateways that enforce authentication need one.

**Model discovery.** FindRomCover lists models by appending `/models` to the configured base URL and accepts either the OpenAI (`data`) or Gemini (`models`) response shape. A custom gateway must expose that endpoint, or **Test / Load Models** fails. If it does not, type the model ID directly into the Model field and skip the test.

### OpenCode Zen

[OpenCode Zen](https://opencode.ai/docs/zen/) is an OpenAI-compatible gateway from the OpenCode team. It currently offers MiMo-V2.5 Free, a multimodal model that accepts image input:

| Setting | Value |
|---------|-------|
| Provider | Custom (OpenAI-compatible) |
| Base URL | `https://opencode.ai/zen/v1` |
| API Key | Zen key from [opencode.ai/auth](https://opencode.ai/auth) |
| Model | `mimo-v2.5-free` |

See [Recommended Models](recommended-models.md) for cost notes and caveats.

## The model picker

Click **Test / Load Models** to:

1. verify the API key and base URL by contacting the provider;
2. download the list of available models;
3. cache the list for the selected provider and base URL for 7 days.

The cache is stored in `%LocalAppData%\FindRomCover\ai-models.json`, keyed by provider and lowercased base URL. If a test fails, the error is shown and the previously cached list stays loaded. Because the cache key includes the base URL, changing the base URL selects a different (usually empty) entry — click **Test / Load Models** to populate it.

The picker:

- shows **vision-capable models only** by default (based on provider modality metadata where available, and a name heuristic otherwise);
- can be filtered with the **filter box** — type part of a model ID to narrow the list;
- displays the number of loaded models;
- falls back to the cached list instantly on later visits.

## How FindRomCover talks to providers

All adapters share one abstraction with identical timeouts, JPEG downscaling, JSON parsing, and friendly error messages:

- OpenAI, GLM, Local, and Custom (OpenAI-compatible) endpoints share one adapter using `/chat/completions`.
- Anthropic and Custom (Anthropic-compatible) endpoints share the native Anthropic adapter using `/messages`.
- Gemini uses its native adapter and calls `generateContent` with inline image data.

Every client requests up to 4,096 output tokens by default. OpenAI reasoning models (`o1`, `o3`, `o4`, `gpt-5*`) use `max_completion_tokens` and omit `temperature`; other models use `temperature` and `max_tokens`. Override these defaults with the [custom request parameters](settings.md#custom-request-parameters) JSON in AI Settings. FindRomCover always prompts for JSON and extracts the JSON object from the response text, so models without a native JSON mode still work.

The local pick prompt asks for the official cover or box art of the exact game and allows the model to answer `-1` when no candidate is genuine cover art. The API-fallback prompt is slightly more lenient: it prefers a game cover or an in-game screenshot over a photo of the physical cartridge, cart, or disc, and accepts an in-game screenshot when no cover art exists. The prompt version participates in the verdict cache key, so prompt changes invalidate old cached answers.

## Related pages

- [AI Settings](settings.md)
- [Picking Covers](picking-covers.md)
- [Recommended Models](recommended-models.md)
- [AI Troubleshooting](troubleshooting.md)
