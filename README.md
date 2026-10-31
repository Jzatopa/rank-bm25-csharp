# RankBm25.CSharp

A small, dependency-free **C# / .NET 10** port of [Dorian Brown's rank_bm25](https://github.com/dorianbrown/rank_bm25): **Okapi BM25, BM25L, and BM25Plus** for in-memory document ranking.

This is an independent fork, not an official upstream release. The original Python source and history are retained. [Original README](docs/UPSTREAM_README.md) · [Compatibility contract](docs/COMPATIBILITY.md) · [Apache-2.0 license](LICENSE)

## Why have this in C# instead of Python?

The benefit is **integration and deployment**, not a new ranking algorithm:

- **Run inside an existing .NET application.** ASP.NET services, desktop tools, and local retrieval pipelines can rank their own tokenized documents without a Python subprocess, Python service, interop bridge, or network round trip.
- **No runtime NuGet dependencies.** The library uses managed dictionaries, arrays, and `Math.Log`; deploying it does not require NumPy or a Python environment.
- **Typed API and normal .NET tooling.** Return your own document types with `GetTopN<T>`, supply a tokenizer delegate, and use the same debugger, build, and deployment process as the rest of your application.
- **Immutable index snapshot.** Corpus data is copied during construction, and scoring uses per-call result buffers. Concurrent reads require no mutable query state.
- **Keep behavior comparable across languages.** Checked-in fixtures are produced by the retained Python implementation, rather than by reimplementing its formulas in a second test oracle.

**Python remains the better choice** when your application already lives in Python/NumPy, or when interactive experimentation and the Python data-science ecosystem matter most. This C# port does not inherently improve relevance, and no speed or memory advantage is claimed without benchmarks. It is not a replacement for a large-scale search engine such as Lucene.NET.

## Quick start

Requires the .NET 10 SDK. The package has **not been published to NuGet**; use a project reference or build a local package.

```bash
git clone https://github.com/Jzatopa/rank-bm25-csharp.git
cd rank-bm25-csharp
dotnet run --project samples/SearchDemo -c Release -- local search
```

```csharp
using RankBm25;

string[] documents = [
    "Hello there good man!",
    "It is quite windy in London",
    "How is the weather today?"
];
var index = new BM25Okapi(documents.Select(text => text.Split(' ')));
string[] query = ["windy", "London"];

double[] scores = index.GetScores(query);
IReadOnlyList<string> best = index.GetTopN(query, documents, n: 1);
// best[0] == "It is quite windy in London"
double[] subset = index.GetBatchScores(query, new[] { 2, 1, 1 });
// Batch order and duplicate IDs are preserved.
```

Swap `BM25Okapi` for `BM25L` or `BM25Plus` to select another variant. Constructors accept named `k1`, `b`, and `epsilon` (Okapi) or `delta` (L/Plus). Use `BM25.Tokenize(documents, tokenizer)` for raw strings.

**Tokenization is your policy.** No implicit lowercasing, stemming, stopword removal, or Unicode normalization is performed. Apply identical preprocessing to documents and queries.

## Build and validate

```bash
dotnet build src/RankBm25 -c Release
dotnet run --project tests/RankBm25.Compatibility -c Release
dotnet pack src/RankBm25 -c Release -o artifacts
```

The compatibility suite is an executable test harness: run it with `dotnet run`, **not** `dotnet test`. It exits nonzero on failure. It checks **1,575 differential cases** from all three variants, plus API/validation checks. Scores use relative/absolute tolerance `1e-12`; distinct-score rankings and batch results are also compared. See the compatibility document for intentional differences and edge cases.

To regenerate reference fixtures and run upstream's tests (Python is needed only for this development step):

```bash
python3 -m venv .venv
.venv/bin/pip install -r scripts/requirements-test.txt
.venv/bin/python scripts/generate_fixtures.py
.venv/bin/pytest -q
```

GitHub Actions regenerates fixtures, checks they are unchanged, runs upstream tests and C# compatibility tests, runs the sample, and builds the NuGet package.

## Scope and attribution

This ports all three **implemented** upstream variants and their full-score, batch-score, and top-N functionality. BM25-Adpt and BM25T are not implemented upstream and are not added here. This remains a simple in-memory scorer, not an inverted-index search server.

Derived from source commit [`47aa3dd`](https://github.com/dorianbrown/rank_bm25/commit/47aa3ddf8dc1ebeb7ef4e65f2b4536af44594099). This fork adds the C# implementation, differential tests, sample, CI, packaging and documentation while leaving `rank_bm25.py` unchanged. See [NOTICE](NOTICE).
