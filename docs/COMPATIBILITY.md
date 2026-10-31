# Compatibility contract

## Reference

- Upstream: https://github.com/dorianbrown/rank_bm25
- Pinned commit: `47aa3ddf8dc1ebeb7ef4e65f2b4536af44594099`
- Python file: retained unchanged at `rank_bm25.py`.
- Oracle environment: Python 3.12, NumPy 2.4.2. Fixture metadata records source SHA-256 and random seed.

The **pinned source**, not a textbook or another BM25 package, defines scoring. In particular, BM25L in this source does **not** multiply the contribution by term frequency outside its normalized-frequency fraction. BM25L and Plus can give a baseline contribution for a known query term even when that document does not contain it. Okapi's negative-IDF replacement can itself remain negative when the average IDF is negative. These behaviors are preserved.

## API mapping

| Python | C# |
| --- | --- |
| `BM25Okapi`, `BM25L`, `BM25Plus` | Same class names, namespace `RankBm25` |
| `get_scores(query)` | `GetScores(query)` → `double[]` |
| `get_batch_scores(query, ids)` | `GetBatchScores(query, ids)` → `double[]` |
| `get_top_n(query, documents, n)` | `GetTopN<T>(query, documents, n)` |
| optional `tokenizer` argument | `BM25.Tokenize(documents, tokenizer)` before construction |
| `corpus_size`, `avgdl`, `doc_len` | `CorpusSize`, `AverageDocumentLength`, `DocumentLengths` |

## Preserved behavior

- Ordinal, case-sensitive token identity; no preprocessing.
- Three scoring formulas, defaults, custom parameters within the supported domain, and repeated query-token contributions.
- Batch IDs preserve order and duplicates.
- Empty queries return zero scores.
- Mixed empty/nonempty documents are allowed. At `b=1`, an empty document may produce NaN exactly as in NumPy. Such degenerate scores are not meaningful retrieval results.
- Binary64 scores agree within `1e-12 * max(1, abs(reference))`, not necessarily bit-for-bit across platforms.

## Deliberate adaptations

- Null documents/tokens are rejected; the API accepts strings, not Python’s arbitrary hashable objects. Constructor positional arguments also differ: Python’s second argument is `tokenizer`, whereas C# uses `k1`; use `BM25.Tokenize` for tokenization.
- Empty corpora and entirely tokenless corpora throw `ArgumentException` instead of upstream's divide-by-zero errors or NaN behavior.
- Parameters require finite `k1 > 0`, `0 <= b <= 1`, and nonnegative finite epsilon/delta. Exotic upstream parameter combinations outside this domain are unsupported.
- Batch IDs must be nonnegative and in range. Python's negative indexing is not supported.
- Negative top-N limits throw rather than invoking Python's negative-slice semantics.
- Score ties are **deterministic: descending original document ID**. NumPy's default `argsort` does not specify portable stable ordering for ties, so exact tie ordering is not promised across languages. .NET places NaN scores after finite scores when ranking; NumPy's reversed argsort can put them first. Avoid `b=1` with empty documents if ranking matters.
- Tokenization is sequential through a delegate, not a Python multiprocessing pool. Choose application-level parallelism if needed.
- Mutable Python attributes are not exposed as mutable C# state. This is a typed API port, not a Python ABI replacement.

## Verification

The seeded fixture generator executes upstream directly for 105 corpora × 3 variants × 5 queries = **1,575 cases**. It covers actual constructor defaults and custom parameters, Unicode, case, empty string tokens, missing terms, repeated terms, negative IDF, single-document corpora, mixed empty documents, repeated/out-of-order batch IDs, and b=0/b=1 boundaries. Distinct finite score rankings are checked against NumPy; ties are tested against the documented C# policy. Additional executable checks cover validation, tokenizer delegates, caller mutation, empty batches, and top-N limits.

This is bounded compatibility evidence, not a proof for every possible input or an exhaustive Unicode conformance claim. Regenerate fixtures when intentionally changing the pinned reference; review diffs before accepting a new baseline.
