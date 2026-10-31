// C# translation of dorianbrown/rank_bm25, pinned in docs/COMPATIBILITY.md.
// Changed: managed arrays, typed API, argument validation and deterministic tie ordering.
// Licensed under Apache-2.0; see LICENSE and NOTICE.
namespace RankBm25;

/// <summary>Immutable tokenized-corpus index shared by the three BM25 variants.</summary>
public abstract class BM25
{
    private readonly Dictionary<string, int>[] frequencies;
    private readonly int[] lengths;
    protected readonly Dictionary<string, double> Idf = new(StringComparer.Ordinal);
    protected readonly Dictionary<string, int> DocumentFrequencies = new(StringComparer.Ordinal);
    protected readonly List<string> Vocabulary = [];
    protected readonly double K1;
    protected readonly double B;
    public int CorpusSize => lengths.Length;
    public double AverageDocumentLength { get; }
    public IReadOnlyList<int> DocumentLengths { get; }

    protected BM25(IEnumerable<IEnumerable<string>> corpus, double k1, double b)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        if (!double.IsFinite(k1) || k1 <= 0) throw new ArgumentOutOfRangeException(nameof(k1));
        if (!double.IsFinite(b) || b < 0 || b > 1) throw new ArgumentOutOfRangeException(nameof(b));
        K1 = k1;
        B = b;
        var docs = corpus.Select(doc =>
        {
            ArgumentNullException.ThrowIfNull(doc);
            return doc.ToArray();
        }).ToArray();
        if (docs.Length == 0 || !docs.Any(doc => doc.Length > 0))
            throw new ArgumentException("Corpus must contain at least one token.", nameof(corpus));
        lengths = docs.Select(doc => doc.Length).ToArray();
        DocumentLengths = Array.AsReadOnly(lengths);
        AverageDocumentLength = lengths.Average();
        frequencies = new Dictionary<string, int>[docs.Length];
        for (int i = 0; i < docs.Length; i++)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var token in docs[i])
            {
                ArgumentNullException.ThrowIfNull(token);
                counts[token] = counts.GetValueOrDefault(token) + 1;
            }
            frequencies[i] = counts;
            foreach (var token in counts.Keys)
            {
                if (!DocumentFrequencies.ContainsKey(token)) Vocabulary.Add(token);
                DocumentFrequencies[token] = DocumentFrequencies.GetValueOrDefault(token) + 1;
            }
        }
    }

    /// <summary>Tokenize raw documents using a caller-supplied, deterministic tokenizer.</summary>
    public static IEnumerable<IEnumerable<string>> Tokenize(IEnumerable<string> documents, Func<string, IEnumerable<string>> tokenizer)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(tokenizer);
        return documents.Select(tokenizer);
    }

    protected static void ValidateNonnegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
    }

    protected abstract double Contribution(double idf, int frequency, double lengthNormalization);

    public double[] GetScores(IEnumerable<string> query) => GetBatchScores(query, Enumerable.Range(0, CorpusSize));

    /// <summary>Score selected document IDs, preserving their order and duplicates.</summary>
    public double[] GetBatchScores(IEnumerable<string> query, IEnumerable<int> documentIds)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(documentIds);
        var ids = documentIds.ToArray();
        if (ids.Any(id => id < 0 || id >= CorpusSize))
            throw new ArgumentOutOfRangeException(nameof(documentIds));
        var scores = new double[ids.Length];
        foreach (var token in query)
        {
            ArgumentNullException.ThrowIfNull(token);
            var idf = Idf.GetValueOrDefault(token);
            for (int j = 0; j < ids.Length; j++)
            {
                int i = ids[j];
                scores[j] += Contribution(idf, frequencies[i].GetValueOrDefault(token), 1 - B + B * lengths[i] / AverageDocumentLength);
            }
        }
        return scores;
    }

    /// <summary>Return supplied documents by score; ties use descending original document ID.</summary>
    public IReadOnlyList<T> GetTopN<T>(IEnumerable<string> query, IReadOnlyList<T> documents, int n = 5)
    {
        ArgumentNullException.ThrowIfNull(documents);
        if (documents.Count != CorpusSize) throw new ArgumentException("Document count must match the corpus.", nameof(documents));
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        var scores = GetScores(query);
        return Enumerable.Range(0, CorpusSize)
            .OrderByDescending(i => scores[i]).ThenByDescending(i => i)
            .Take(n).Select(i => documents[i]).ToArray();
    }
}
