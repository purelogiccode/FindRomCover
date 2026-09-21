# Recommended Vision Models (OpenRouter)

> Also available in the documentation: [Recommended Models](https://purelogiccode.github.io/FindRomCover/ai/recommended-models/)

Cheapest vision-capable models suitable for FindRomCover's AI Assist (synchronous chat completions, image input, text/JSON output).

Source: OpenRouter `GET /api/v1/models` — 446 models total, 275 accept image input (checked 2026-09-20).
Prices are USD per million tokens (input / output), ordered by input price. FindRomCover's current OpenRouter default is **`qwen/qwen3.7-flash`**.

## Models

| # | Model ID | Input $/M | Output $/M | Context | JSON mode | Notes |
|---|----------|-----------|------------|---------|-----------|-------|
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

Estimated cost per 1,000 cover picks (assuming ~6 images ≈ 6k prompt tokens + ~200 completion tokens):

- `amazon/nova-lite-v1` ≈ $0.41

## Caveats

- Models without native JSON mode still work: FindRomCover prompts for JSON and parses it from the response text.
- Image token cost is provider-dependent; the estimates above assume ~1,000 tokens per 512px thumbnail.

## How to use

In **AI Settings → Provider: OpenRouter**, paste the model ID into the Model box (the picker's filter can find it; all listed models are flagged vision-capable) and click **Test / Load Models**.
