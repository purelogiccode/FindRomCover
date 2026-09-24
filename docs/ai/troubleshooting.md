# AI Troubleshooting

This page covers problems specific to AI Vision Assist. For general application issues see [Troubleshooting](../troubleshooting.md).

## The AI returns an empty response

**Cause:** Some models are reasoning models — they spend output tokens thinking before writing an answer. Older builds capped the output at 400 tokens, which a reasoning model can consume entirely, leaving no text and a `length` finish reason.

**Status:** Fixed. FindRomCover now allows up to 4,096 output tokens and raises a descriptive error if a model still exhausts its budget, for example:

> AI model ran out of output tokens before producing an answer. Reasoning models can spend the whole output budget on internal thinking; try a non-reasoning model (for example google/gemma-3-12b-it).

**What to do:**

- Use a model that is not a heavy reasoner for this task (for example `google/gemma-4-26b-a4b-it` or `gemma-3-12b-it`).
- If the error persists with a reasoning model, choose a different model — the output budget is not user-configurable.

## "API key is invalid" or 401/403 errors

| Provider | Checks |
|----------|--------|
| OpenRouter | Key from [openrouter.ai/keys](https://openrouter.ai/keys); check remaining credits |
| OpenAI | Key from [platform.openai.com/api-keys](https://platform.openai.com/api-keys); check project billing |
| Anthropic | Key from [console.anthropic.com](https://console.anthropic.com) |
| Gemini | Key from [aistudio.google.com/apikey](https://aistudio.google.com/apikey); the API must be enabled for the key |
| GLM | Key from [z.ai](https://z.ai); for the China endpoint set the Base URL to `https://open.bigmodel.cn/api/paas/v4` |
| Local | No key needed; make sure the server is running and the Base URL/port are correct |
| Custom | Verify the Base URL and whether the endpoint expects an API key |

## "Test / Load Models" fails

1. Check the Base URL — it must include the API version, for example `https://openrouter.ai/api/v1`.
2. Verify the key is pasted completely, with no leading or trailing spaces.
3. For local servers, confirm the server is running (`ollama serve`) and reachable at the configured port.
4. Check the [log window](../configuration/logs.md) for the exact HTTP status and message.

A cached model list can still be used if the provider is temporarily offline — the list is kept for 7 days.

## Rate limits and quota errors (429)

- **OpenRouter free tier:** about 50 requests/day without credits, about 1,000/day after a one-time $10 credit purchase.
- **Gemini:** free-tier limits are per model and per minute; slow down batch runs.
- **Google Custom Search API:** 100 queries/day on the free tier; this affects the API fallback in Batch Fill.

Wait for the quota to reset, enable billing, or switch providers.

## The model cannot see images

Symptoms: the model ignores the image, describes only the text, or returns nonsense.

- Open **Test / Load Models** and keep **Vision-capable only** checked — non-vision models are hidden.
- Some providers report modality metadata inaccurately; if a model consistently fails, try another.
- Local models must be multimodal; `qwen2.5vl:7b` is a good default.

## Timeouts

| Situation | Fix |
|-----------|-----|
| Slow local model | Raise **Timeout** to 180–300 seconds in [AI Settings](settings.md) |
| Large images | Lower **Image max size** to 384 px |
| Many candidates | Lower **Max candidates sent** |
| Unstable network | Retry; check the log for timeout details |

## AI finds no candidates

- Lower **Candidate similarity** in AI Settings (for example to 50).
- Lower the main window similarity threshold.
- Check that the game name is meaningful; enable `Settings > Use MAME Descriptions` for arcade titles.
- For the Google API tab, verify the API key and quota.

## Auto-save never happens

- Check the confidence shown in the result; raise or lower **Auto-save threshold** as appropriate.
- Ensure **Automatically save the AI pick** is enabled.
- Verify the image folder is writable and the log shows no save errors.

## The same cover is queried repeatedly

- Enable **Skip missing covers already queried in previous sessions** in AI Settings.
- Batch Fill skips items in the history and reports them as **SkippedAlreadyQueried**.
- Clear the history with **Clear query history** if you want a fresh attempt.

## High costs

- Use the [recommended cheapest models](recommended-models.md).
- Keep **Max candidates sent** low and **Candidate similarity** high.
- Leave the verdict cache enabled (30 days).
- Keep the query history enabled (180 days).
- Disable the Google API fallback in Batch Fill unless you need it.

## Where to look next

- [Logs & Diagnostics](../configuration/logs.md) — exact error messages
- [Providers & Models](providers.md) — correct base URLs and defaults
- [AI Settings](settings.md) — every setting explained
- [Recommended Models](recommended-models.md) — cheaper or stronger models
