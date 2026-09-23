# Recommended Vision Models (OpenRouter)

> Also available in the documentation: [Recommended Models](https://purelogiccode.github.io/FindRomCover/ai/recommended-models/)

Cheapest vision-capable models suitable for FindRomCover's AI Assist (synchronous chat completions, image input, text/JSON output).

## Models

google/gemma-4-26b-a4b-it
amazon/nova-lite-v1


## Caveats

- Models without native JSON mode still work: FindRomCover prompts for JSON and parses it from the response text.
- Image token cost is provider-dependent; the estimates above assume ~1,000 tokens per 512px thumbnail.

## How to use

In **AI Settings → Provider: OpenRouter**, paste the model ID into the Model box (the picker's filter can find it; all listed models are flagged vision-capable) and click **Test / Load Models**.
