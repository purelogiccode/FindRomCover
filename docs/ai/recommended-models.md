# Recommended Models

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
| 5 | `amazon/nova-lite-v1` | $0.06 | $0.24 | 300k | prompt-only JSON | Solid low-cost multimodal; 5k max output tokens |

Estimated cost per 1,000 cover picks (assuming about 6 images ≈ 6k prompt tokens + about 200 completion tokens):

| Model | Estimated cost per 1,000 picks |
|-------|-------------------------------|
| `qwen/qwen3.7-flash` | ≈ **$0.21** |
| `google/gemma-3-4b-it` | ≈ $0.32 |
| `google/gemma-3-12b-it` | ≈ $0.33 |
| `openai/gpt-5-nano` | ≈ $0.38 |
| `amazon/nova-lite-v1` | ≈ $0.41 |

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

OpenRouter's free tier is rate-limited (about 50 requests/day without credits, about 1,000/day after a one-time $10 credit purchase) and can be flaky — fine for trying AI Assist, not for bulk runs.

## Caveats

- `:batch` variants are cheaper but asynchronous and do not work with FindRomCover's synchronous client.
- Models without native JSON mode still work: FindRomCover prompts for JSON and parses it from the response text.
- Image token cost is provider-dependent; the estimates above assume about 1,000 tokens per 512px thumbnail.
- Reasoning models may spend output tokens on internal reasoning before producing text. FindRomCover allows up to 4,096 output tokens and reports a clear error if a model still exhausts the budget.

## How to use

1. Open `Settings > AI Settings...`.
2. Select **OpenRouter** and paste your API key.
3. Click **Test / Load Models**.
4. Type a model ID from this page into the filter box and select it.
5. Save, then use **AI Pick Best** or **Batch Fill**.

All models listed here are flagged vision-capable by OpenRouter's modality metadata, so they appear when **Vision-capable only** is checked.

## Choosing a model

| Priority | Recommendation |
|----------|----------------|
| Lowest cost | `qwen/qwen3.7-flash` |
| Most reliable JSON output | `openai/gpt-5-nano` |
| Best quality per dollar | `google/gemma-3-12b-it` or `google/gemini-2.5-flash-lite` |
| No cloud cost | A local model via Ollama, for example `qwen2.5vl:7b` |

> **Note:** Prices change frequently. Verify current pricing on your provider's website before running large batches.

## Related pages

- [Providers & Models](providers.md)
- [AI Settings](settings.md)
- [Batch Fill](batch-fill.md)
