# Similarity Algorithms

Local Files search compares the cleaned ROM name with every image filename in your image folder. Three algorithms are available, selectable under `Set Similarity Algorithm`.

## The algorithms

### Jaro-Winkler Distance (default)

Measures how similar two strings are, with extra weight for a common prefix. It is forgiving of small differences and transpositions, which makes it a good default for game names that share prefixes such as series titles.

- **Best for:** most collections
- **Strengths:** handles typos, punctuation differences, and short suffixes
- **Weaknesses:** can over-match names that share a long prefix

### Jaccard Similarity

Compares the sets of character n-grams (trigrams) in both names: the size of the intersection divided by the size of the union.

- **Best for:** filenames with reordered words, for example `Mario Bros, The` vs `The Mario Bros`
- **Strengths:** order-insensitive, fast with the trigram index
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

## Filename cleaning

Before comparison, FindRomCover cleans the search name:

- removes file extensions;
- strips region and revision tags such as `(USA)`, `(Europe)`, `[!]`;
- replaces separators (`_`, `-`, `.`) with spaces;
- collapses repeated whitespace;
- sanitizes characters that are unsafe in filenames.

The result is used for similarity scoring, web searches, and AI prompts. When `Use MAME Descriptions` is enabled, the MAME description replaces the cleaned filename.

## Performance

For image folders with 50 or more files, a trigram index pre-filters candidates before the full similarity comparison. The index is built per search and keeps large collections responsive.

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
