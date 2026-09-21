# Recommended Models

Cheapest vision-capable models suitable for FindRomCover's AI Assist (synchronous chat completions, image input, text/JSON output).

Source: OpenRouter `GET /api/v1/models` — 446 models total, 275 accept image input (checked 2026-09-20).
Prices are USD per million tokens (input / output), ordered by input price. FindRomCover's current OpenRouter default is **`qwen/qwen3.7-flash`**.

## Models

| # | Model ID | Input $/M | Output $/M | Context | JSON mode | Notes |
|---|----------|-----------|------------|---------|-----------|-------|
| 1 | `qwen/qwen3.7-flash` | $0.03 | $0.13 | 1M | `response_format` | Cheapest overall; vision-language reasoning model from Alibaba; ~$0.21 per 1,000 picks |
| 2 | `google/gemma-3-4b-it` | $0.05 | $0.10 | 131k | `response_format` | Cheapest output tokens; small but genuinely multimodal |
| 3 | `google/gemma-3-12b-it` | $0.05 | $0.15 | 131k | `response_format` | Same input price as 4B with better quality |
| 4 | `openai/gpt-5-nano` | $0.05 | $0.40 | 400k | structured outputs + `response_format` | Most reliable JSON parsing; smallest GPT-5 variant |
| 5 | `inclusionai/ling-3.0-flash-vl` | $0.06 | $0.18 | 131k | `response_format` | Vision-language specialist (124B MoE / 5.5B active) |
| 6 | `amazon/nova-lite-v1` | $0.06 | $0.24 | 300k | prompt-only JSON | **Proven for cover picking** — very reliable in real batch runs; solid low-cost multimodal; 5k max output tokens |
| 7 | `qwen/qwen3.5-flash-02-23` | $0.065 | $0.26 | 1M | `response_format` | 1M context |
| 8 | `~z-ai/glm-flash-latest` | $0.075 | $0.25 | 1.3M | `response_format` | 1.3M context |
| 9 | `bytedance-seed/seed-1.6-flash` | $0.075 | $0.30 | 262k | `response_format` | 262k context |
| 10 | `google/gemma-3-27b-it` | $0.08 | $0.45 | 131k | `response_format` | Larger Gemma 3 |
| 11 | `mistralai/mistral-small-3.2-24b-instruct` | $0.09 | $0.25 | 256k | `response_format` | 256k context |
| 12 | `google/gemma-4-26b-a4b-it` | $0.09 | $0.30 | 262k | `response_format` | MoE variant of Gemma 4 |
| 13 | `google/gemma-4-31b-it` | $0.09 | $0.34 | 262k | `response_format` | Best value of the Gemma 4 family |
| 14 | `qwen/qwen3.5-9b` | $0.10 | $0.15 | 262k | `response_format` | 262k context; text, image and video input |
| 15 | `meta-llama/llama-4-scout` | $0.10 | $0.30 | 1.3M | `response_format` | 1.3M context |
| 16 | `google/gemini-2.5-flash-lite` | $0.10 | $0.40 | 1M | `response_format` | Known-good quality; per-image pricing |
| 17 | `qwen/qwen3.8-27b` | $0.20 | $2.50 | 1M | `response_format` | Large Qwen vision model |
| 18 | `thinkingmachines/inkling-small` | $0.45 | $1.20 | 1.05M | prompt-only JSON | Multimodal, also accepts audio |
| 19 | `thinkingmachines/inkling` | $1.00 | $4.05 | 1.05M | prompt-only JSON | Multimodal, also accepts audio |

Estimated cost per 1,000 cover picks (assuming about 6 images ≈ 6k prompt tokens + about 200 completion tokens):

| Model | Estimated cost per 1,000 picks |
|-------|-------------------------------|
| `qwen/qwen3.7-flash` | ≈ **$0.21** |
| `google/gemma-3-4b-it` | ≈ $0.32 |
| `google/gemma-3-12b-it` | ≈ $0.33 |
| `openai/gpt-5-nano` | ≈ $0.38 |
| `amazon/nova-lite-v1` | ≈ $0.41 |

## OpenCode Zen (free MiMo-V2.5)

[OpenCode Zen](https://opencode.ai/docs/zen/) is the OpenCode team's AI gateway. It is OpenAI-compatible and currently offers **MiMo-V2.5 Free** (`mimo-v2.5-free`) at no cost for a limited time. MiMo-V2.5 accepts image input, so it works with FindRomCover.

| Setting | Value |
|---------|-------|
| Provider | **Custom (OpenAI-compatible)** |
| Base URL | `https://opencode.ai/zen/v1` |
| API Key | Your Zen key from [opencode.ai/auth](https://opencode.ai/auth) |
| Model | `mimo-v2.5-free` |

Notes:

- Free for a limited time; Zen states that data from MiMo-V2.5 Free may be used to improve the model while it is free.
- Zen's model metadata may not report image modality, so the picker's **Vision-capable only** filter can hide it — uncheck the filter or type the model ID directly.
- Zen also serves paid vision models, for example `deepseek-v4-flash-vision-exp` ($0.14 / $0.28 per M tokens).
- In OpenCode itself the model is referenced as `opencode/mimo-v2.5-free`; FindRomCover uses the raw model ID `mimo-v2.5-free`.

## Caveats

- `:free` variants exist for several models above but are rate-limited and frequently unavailable — use the paid endpoints for batch runs.
- `:batch` variants are cheaper but asynchronous and do not work with FindRomCover's synchronous client.
- Models without native JSON mode still work: FindRomCover prompts for JSON and parses it from the response text.
- Image token cost is provider-dependent; the estimates above assume about 1,000 tokens per 512px thumbnail.
- Reasoning models may spend output tokens on internal reasoning before producing text. FindRomCover allows up to 4,096 output tokens and reports a clear error if a model still exhausts the budget.

## How to use

1. Open `Settings > AI Settings...`.
2. Select **OpenRouter** and paste your API key.
3. Click **Test / Load Models**.
4. Type a model ID from the table into the filter box and select it.
5. Save, then use **AI Pick Best** or **Batch Fill**.

All models listed here are flagged vision-capable by OpenRouter's modality metadata, so they appear when **Vision-capable only** is checked.

For **OpenCode Zen**, choose Provider: **Custom (OpenAI-compatible)**, set the Base URL to `https://opencode.ai/zen/v1`, paste your Zen key, and enter `mimo-v2.5-free` as the model.

## Choosing a model

| Priority | Recommendation |
|----------|----------------|
| Lowest cost | `qwen/qwen3.7-flash` |
| Most reliable JSON output | `openai/gpt-5-nano` |
| Best quality per dollar | `google/gemma-3-12b-it` or `google/gemini-2.5-flash-lite` |
| Best Gemma 4 value | `google/gemma-4-31b-it` |
| **Proven for cover picking** | `amazon/nova-lite-v1` — very reliable in real batch runs |
| Free (OpenCode Zen, limited time) | `mimo-v2.5-free` |
| No cloud cost | A local model via Ollama, for example `qwen2.5vl:7b` |

> **Note:** Prices change frequently. Verify current pricing on your provider's website before running large batches.

## Related pages

- [Providers & Models](providers.md)
- [AI Settings](settings.md)
- [Batch Fill](batch-fill.md)
