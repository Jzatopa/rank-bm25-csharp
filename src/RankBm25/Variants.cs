// C# translations of the pinned rank_bm25 scoring formulas. Apache-2.0.
// Changed: typed immutable API and explicit parameter validation; see NOTICE.
// This fork is maintained through agentic AI under Jzatopa's direction.
// Changes are validated against the Python reference through compatibility tests and CI.
namespace RankBm25;

public sealed class BM25Okapi : BM25
{
    public BM25Okapi(IEnumerable<IEnumerable<string>> corpus, double k1 = 1.5, double b = 0.75, double epsilon = 0.25)
        : base(corpus, k1, b)
    {
        ValidateNonnegative(epsilon, nameof(epsilon));
        double sum = 0;
        foreach (var token in Vocabulary)
        {
            int count = DocumentFrequencies[token];
            double idf = Math.Log(CorpusSize - count + 0.5) - Math.Log(count + 0.5);
            Idf[token] = idf;
            sum += idf;
        }
        double floor = epsilon * (sum / Idf.Count);
        foreach (var token in Idf.Keys.ToArray())
            if (Idf[token] < 0) Idf[token] = floor;
    }
    protected override double Contribution(double idf, int frequency, double lengthNormalization) =>
        idf * (frequency * (K1 + 1) / (frequency + K1 * lengthNormalization));
}

public sealed class BM25L : BM25
{
    private readonly double delta;
    public BM25L(IEnumerable<IEnumerable<string>> corpus, double k1 = 1.5, double b = 0.75, double delta = 0.5)
        : base(corpus, k1, b)
    {
        ValidateNonnegative(delta, nameof(delta));
        this.delta = delta;
        foreach (var token in Vocabulary)
            Idf[token] = Math.Log(CorpusSize + 1) - Math.Log(DocumentFrequencies[token] + 0.5);
    }
    protected override double Contribution(double idf, int frequency, double lengthNormalization)
    {
        double ctd = frequency / lengthNormalization;
        return idf * (K1 + 1) * (ctd + delta) / (K1 + ctd + delta);
    }
}

public sealed class BM25Plus : BM25
{
    private readonly double delta;
    public BM25Plus(IEnumerable<IEnumerable<string>> corpus, double k1 = 1.5, double b = 0.75, double delta = 1)
        : base(corpus, k1, b)
    {
        ValidateNonnegative(delta, nameof(delta));
        this.delta = delta;
        foreach (var token in Vocabulary)
            Idf[token] = Math.Log(CorpusSize + 1) - Math.Log(DocumentFrequencies[token]);
    }
    protected override double Contribution(double idf, int frequency, double lengthNormalization) =>
        idf * (delta + frequency * (K1 + 1) / (K1 * lengthNormalization + frequency));
}
