# Recommended Vision Models (OpenRouter)

> Also available in the documentation with current prices: [Recommended Models](https://purelogiccode.github.io/FindRomCover/ai/recommended-models/)

Vision-capable models suitable for FindRomCover's AI Assist (synchronous chat completions, image input, text/JSON output). The application's default model is `qwen/qwen3.7-flash`.

## Models

qwen/qwen3.7-flash
google/gemma-4-26b-a4b-it
amazon/nova-lite-v1

## Caveats

- Models without native JSON mode still work: FindRomCover prompts for JSON and parses it from the response text.
- Image token cost is provider-dependent; assume roughly 1,000 tokens per 512px thumbnail. See the documentation page for current prices.

## How to use

In **AI Settings → Provider: OpenRouter**, paste the model ID into the Model box (the picker's filter can find it; the listed models are flagged vision-capable) and click **Test / Load Models**.
