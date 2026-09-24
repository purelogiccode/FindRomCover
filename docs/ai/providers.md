# Providers & Models

FindRomCover supports cloud providers, local servers, and custom endpoints. All of them are configured in `Settings > AI Settings...`.

## Provider comparison

| Provider | Base URL | Default model | Key required | Notes |
|----------|----------|---------------|--------------|-------|
| OpenRouter | `https://openrouter.ai/api/v1` | `qwen/qwen3.7-flash` | Yes | Aggregator with hundreds of models; cheapest vision options |
| OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` | Yes | Reliable JSON output; `gpt-5-nano` is a low-cost alternative |
| Anthropic | `https://api.anthropic.com/v1` | `claude-sonnet-4-5` | Yes | Strong visual reasoning; native Messages API |
| Gemini | `https://generativelanguage.googleapis.com/v1beta` | `gemini-2.5-flash` | Yes | Generous free tier; `gemini-2.5-flash-lite` is cheaper |
| GLM | `https://api.z.ai/api/paas/v4` | `glm-4.5v` | Yes | Zhipu/Z.AI; use `https://open.bigmodel.cn/api/paas/v4` for the China endpoint |
| Local | `http://localhost:11434/v1` | `qwen2.5vl:7b` | No | Ollama or LM Studio; no cloud cost, slower on weak hardware |
| Custom (OpenAI-compatible) | User-defined | User-defined | Optional | Proxies, gateways, self-hosted servers |
| Custom (Anthropic-compatible) | User-defined | User-defined | Optional | Endpoints speaking the Anthropic Messages API |

## OpenRouter

1. Create an API key at [openrouter.ai/keys](https://openrouter.ai/keys).
2. Select **OpenRouter** in AI Settings, paste the key, and click **Test / Load Models**.
3. Pick a model. The default `qwen/qwen3.7-flash` is the cheapest vision-capable model on OpenRouter at the time of writing.

OpenRouter exposes modality metadata, so the model picker knows exactly which models accept image input. See [Recommended Models](recommended-models.md) for a cost-ranked list.

## OpenAI

1. Create an API key at [platform.openai.com/api-keys](https://platform.openai.com/api-keys).
2. Select **OpenAI**, paste the key, and click **Test / Load Models**.
3. Choose a vision-capable model such as `gpt-4o-mini` or `gpt-5-nano`.

The OpenAI adapter uses the shared OpenAI-compatible client. Reasoning models (`o1`, `o3`, `o4`, `gpt-5*`) are called without `temperature` and with `max_completion_tokens`; other models use `temperature` and `max_tokens`. The app always prompts for JSON and parses it from the response text.

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

| Option | API style | When to use |
|--------|-----------|-------------|
| Custom (OpenAI-compatible) | `/chat/completions` | Anything that speaks the OpenAI API, including Azure OpenAI gateways and LiteLLM |
| Custom (Anthropic-compatible) | `/messages` | Endpoints that speak the Anthropic Messages API |

Enter the **Base URL** and **Model** manually. The API key is optional — some self-hosted servers ignore it.

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

The picker:

- shows **vision-capable models only** by default (based on provider modality metadata where available, and a name heuristic otherwise);
- can be filtered with the **filter box** — type part of a model ID to narrow the list;
- displays the number of loaded models;
- falls back to the cached list instantly on later visits.

If the provider is offline or the key is invalid, an error message explains the problem and no list is loaded.

## How FindRomCover talks to providers

All adapters share one abstraction with identical prompts, JSON parsing, timeouts, and friendly error messages. OpenAI, GLM, local, and custom OpenAI-compatible endpoints share one adapter; Anthropic and custom Anthropic-compatible endpoints share the native Anthropic adapter; Gemini uses its native adapter.

Images are downscaled before upload, and responses are parsed as JSON. Models without native JSON mode still work — FindRomCover extracts JSON from the response text.

## Related pages

- [AI Settings](settings.md)
- [Recommended Models](recommended-models.md)
- [AI Troubleshooting](troubleshooting.md)
