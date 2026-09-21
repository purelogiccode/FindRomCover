# Recommended Vision Models (OpenRouter)

> Also available in the documentation: [Recommended Models](https://purelogiccode.github.io/FindRomCover/ai/recommended-models/)

Cheapest vision-capable models suitable for FindRomCover's AI Assist (synchronous chat completions, image input, text/JSON output).

Source: OpenRouter `GET /api/v1/models` — 446 models total, 275 accept image input (checked 2026-09-20).
Prices are USD per million tokens (input / output). FindRomCover's current OpenRouter default is **`qwen/qwen3.7-flash`**.

## Top 5 cheapest

| # | Model ID | Input $/M | Output $/M | Context | JSON mode | Notes |
|---|----------|-----------|------------|---------|-----------|-------|
| 1 | `qwen/qwen3.7-flash` | $0.03 | $0.13 | 1M | `response_format` | Cheapest overall; vision-language reasoning model from Alibaba; ~$0.21 per 1,000 picks |
| 2 | `openai/gpt-5-nano` | $0.05 | $0.40 | 400k | structured outputs + `response_format` | Most reliable JSON parsing; smallest GPT-5 variant |
| 3 | `google/gemma-3-4b-it` | $0.05 | $0.10 | 131k | structured outputs | Cheapest output tokens; small but genuinely multimodal |
| 4 | `google/gemma-3-12b-it` | $0.05 | $0.15 | 131k | structured outputs | Same input price as 4B with better quality |
| 5 | `amazon/nova-lite-v1` | $0.06 | $0.24 | 300k | prompt-only JSON | **Proven for cover picking** — very reliable in real batch runs; solid low-cost multimodal; 5k max output tokens |

Estimated cost per 1,000 cover picks (assuming ~6 images ≈ 6k prompt tokens + ~200 completion tokens):

- `qwen/qwen3.7-flash` ≈ **$0.21**
- `google/gemma-3-4b-it` ≈ $0.32
- `google/gemma-3-12b-it` ≈ $0.33
- `openai/gpt-5-nano` ≈ $0.38
- `amazon/nova-lite-v1` ≈ $0.41

## Runner-ups

| Model ID | Input $/M | Output $/M | Notes |
|----------|-----------|------------|-------|
| `inclusionai/ling-3.0-flash-vl` | $0.06 | $0.18 | Vision-language specialist (124B MoE / 5.5B active) |
| `qwen/qwen3.5-flash-02-23` | $0.07 | $0.26 | 1M context |
| `~z-ai/glm-flash-latest` | $0.07 | $0.25 | 1.3M context |
| `bytedance-seed/seed-1.6-flash` | $0.07 | $0.30 | 262k context |
| `google/gemma-3-27b-it` | $0.08 | $0.45 | Larger Gemma 3 |
| `mistralai/mistral-small-3.2-24b-instruct` | $0.09 | $0.25 | 256k context |
| `meta-llama/llama-4-scout` | $0.10 | $0.30 | 1.3M context |
| `google/gemini-2.5-flash-lite` | $0.10 | $0.40 | Known-good quality, per-image pricing |

## Free options (testing only)

`inclusionai/ling-3.0-flash-vl:free`, `google/gemma-4-31b-it:free`, `google/gemma-4-26b-a4b-it:free`, `qwen/qwen3.8-27b:free`

OpenRouter free tier is rate-limited (~50 requests/day without credits, ~1,000/day after a one-time $10 credit purchase) and can be flaky — fine for trying AI Assist, not for bulk runs.

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

- `:batch` variants are cheaper but asynchronous and do not work with FindRomCover's synchronous client.
- Models without native JSON mode still work: FindRomCover prompts for JSON and parses it from the response text.
- Image token cost is provider-dependent; the estimates above assume ~1,000 tokens per 512px thumbnail.

## How to use

In **AI Settings → Provider: OpenRouter**, paste the model ID into the Model box (the picker's filter can find it; all listed models are flagged vision-capable) and click **Test / Load Models**.

For **OpenCode Zen**, choose Provider: **Custom (OpenAI-compatible)**, set the Base URL to `https://opencode.ai/zen/v1`, paste your Zen key, and enter `mimo-v2.5-free` as the model.
