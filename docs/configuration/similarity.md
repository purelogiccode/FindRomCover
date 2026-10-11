# Similarity Algorithms

Local Files search compares the cleaned ROM name with every image filename in your image folder. Three algorithms are available, selectable under `Set Similarity Algorithm`. The default is **Jaro-Winkler Distance** with a threshold of **70%**.

## The algorithms

### Jaro-Winkler Distance (default)

Measures how similar two strings are, with extra weight for a common prefix. It is forgiving of small differences and transpositions, which makes it a good default for game names that share prefixes such as series titles.

- **Best for:** most collections
- **Strengths:** handles typos, punctuation differences, and short suffixes
- **Weaknesses:** can over-match names that share a long prefix

### Jaccard Similarity

Compares the sets of character **bigrams** (two-character sequences) in both names: the size of the intersection divided by the size of the union. Names shorter than two characters fall back to single characters (unigrams). Trigrams are used only by the separate pre-filter index described under [Performance](#performance), not by the score itself.

- **Best for:** filenames with reordered words, for example `Mario Bros, The` vs `The Mario Bros`
- **Strengths:** order-insensitive; the trigram pre-filter keeps large folders responsive
- **Weaknesses:** less sensitive to small edits than Levenshtein

### Levenshtein Distance

Counts the minimum number of insertions, deletions, and substitutions needed to turn one name into the other, then converts that distance into a similarity percentage.

- **Best for:** strict edit-distance matching
- **Strengths:** intuitive, well suited to near-identical names
- **Weaknesses:** longer names can produce lower scores even when they look similar; an early-exit optimization keeps large collections fast

## The similarity threshold

`Set Similarity Threshold` sets the minimum score (10%–90%, default 70%) required for an image to appear in the results.

| Threshold | Effect |
|-----------|--------|
| 90% | Only near-identical names |
| 70% (default) | Balanced precision and recall |
| 50% | Tolerant of abbreviations and missing words |
| 10–30% | Very permissive; expect noise |

Lower thresholds produce longer result lists. The same threshold pre-filters AI candidates in the main window; AI Settings has a separate **Candidate similarity** value.

## Ignoring bracketed text

`Ignore Bracketed Text in Matching` (enabled by default) removes balanced bracketed groups — `(USA)`, `[En]`, `{Rev 1}`, including nested groups — from both the search name and the image filenames before they are scored. This lets a ROM named `Game (USA) [En]` match a cover simply named `Game`.

Turn the menu item off to score the full filenames exactly as they are.

## Filename cleaning

The local matcher compares the ROM name (or the MAME description when **Use MAME Descriptions** is on) against every image filename in the folder:

- file extensions are removed from image filenames;
- text inside `()`, `[]`, and `{}` is ignored when **Ignore Bracketed Text in Matching** is on (the default);
- names are compared case-insensitively.

For web and API searches, the query is built from the cleaned name: bracketed region, revision, and language tags such as `(USA)`, `(Europe)`, and `[!]` are removed. When a cover is saved, `SanitizeFileName` replaces characters that are invalid in filenames.

## Performance

For image folders with **more than 50 files**, a trigram index pre-filters candidates before the full similarity comparison. The index is used only when it returns at least **5% of all files**; if it returns fewer candidates, the full scan runs instead. The index is built per search and keeps large collections responsive.

Additional optimizations:

- Levenshtein distance stops early once the minimum possible distance exceeds the threshold.
- Jaccard n-grams are computed once per query.
- Jaro-Winkler reuses pooled buffers to reduce garbage collection.

## Choosing an algorithm

| Situation | Recommendation |
|-----------|----------------|
| You do not know where to start | Jaro-Winkler at 70% |
| Filenames use different word order | Jaccard |
| Covers differ by one or two characters | Levenshtein |
| AI picks are enabled | Jaro-Winkler with a lower AI **Candidate similarity** |

## Related pages

- [Local Files Search](../user-guide/local-files.md)
- [AI Settings](../ai/settings.md)
- [Search performance](../user-guide/folders-and-scanning.md)
