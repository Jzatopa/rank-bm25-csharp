using System.Text.Json;
using System.Security.Cryptography;
using RankBm25;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}
void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { assertions++; return; }
    throw new Exception($"Expected {typeof(T).Name}");
}
void Compare(double[] actual, JsonElement expected, string label)
{
    Check(actual.Length == expected.GetArrayLength(), label + " length");
    int i = 0;
    foreach (var item in expected.EnumerateArray())
    {
        var wanted = item.ValueKind == JsonValueKind.Number ? item.GetDouble() : item.GetString() switch
        {
            "nan" => double.NaN, "inf" => double.PositiveInfinity, "-inf" => double.NegativeInfinity,
            _ => throw new Exception("Unrecognized nonfinite oracle value")
        };
        double got = actual[i++];
        Check(double.IsNaN(wanted) ? double.IsNaN(got) : double.IsInfinity(wanted) ? got == wanted :
            double.IsFinite(got) && Math.Abs(got - wanted) <= 1e-12 * Math.Max(1, Math.Abs(wanted)),
            $"{label}[{i-1}]: wanted {wanted:R}, got {got:R}");
    }
}

using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures.json")));
Check(json.RootElement.GetProperty("sourceCommit").GetString() == "47aa3ddf8dc1ebeb7ef4e65f2b4536af44594099", "pinned oracle commit");
var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "oracle-source.py")))).ToLowerInvariant();
Check(sourceHash == json.RootElement.GetProperty("sourceSha256").GetString(), "oracle source hash");
int cases = 0, rankingChecks = 0;
foreach (var test in json.RootElement.GetProperty("cases").EnumerateArray())
{
    var corpus = test.GetProperty("corpus").Deserialize<string[][]>()!;
    var query = test.GetProperty("query").Deserialize<string[]>()!;
    var p = test.GetProperty("parameters");
    bool defaults = !p.EnumerateObject().Any();
    double k1 = defaults ? 1.5 : p.GetProperty("k1").GetDouble(), b = defaults ? 0.75 : p.GetProperty("b").GetDouble();
    BM25 index = test.GetProperty("variant").GetString() switch
    {
        "okapi" when defaults => new BM25Okapi(corpus),
        "l" when defaults => new BM25L(corpus),
        "plus" when defaults => new BM25Plus(corpus),
        "okapi" => new BM25Okapi(corpus, k1, b, p.GetProperty("epsilon").GetDouble()),
        "l" => new BM25L(corpus, k1, b, p.GetProperty("delta").GetDouble()),
        _ => new BM25Plus(corpus, k1, b, p.GetProperty("delta").GetDouble())
    };
    string name = test.GetProperty("name").GetString()!;
    Compare(index.GetScores(query), test.GetProperty("scores"), name);
    Compare(index.GetBatchScores(query, test.GetProperty("ids").Deserialize<int[]>()!), test.GetProperty("batch"), name + " batch");
    if (test.GetProperty("top").ValueKind == JsonValueKind.Array)
    {
        rankingChecks++;
        Check(index.GetTopN(query, Enumerable.Range(0, corpus.Length).ToArray(), corpus.Length)
            .SequenceEqual(test.GetProperty("top").Deserialize<int[]>()!), name + " order");
    }
    Check(index.CorpusSize == corpus.Length, name + " corpus size");
    Check(index.DocumentLengths.SequenceEqual(corpus.Select(x => x.Length)), name + " lengths");
    cases++;
}

Check(cases == 1575, "complete fixture count");

string[][] docs = [["cat"], ["dog"], ["bird"]];
var bm = new BM25Okapi(docs);
Check(bm.GetTopN(Array.Empty<string>(), new[] {0, 1, 2}).SequenceEqual(new[] {2, 1, 0}), "deterministic ties");
Check(bm.GetTopN(new[] {"cat"}, docs, 0).Count == 0, "zero limit");
Check(bm.GetTopN(new[] {"cat"}, docs, 99).Count == 3, "large limit");
Check(bm.GetBatchScores(new[] {"cat"}, Array.Empty<int>()).Length == 0, "empty batch");
Throws<ArgumentException>(() => new BM25Okapi(Array.Empty<string[]>()));
Throws<ArgumentException>(() => new BM25L(new[] { Array.Empty<string>() }));
Throws<ArgumentOutOfRangeException>(() => new BM25Plus(docs, k1: 0));
Throws<ArgumentOutOfRangeException>(() => new BM25Okapi(docs, b: double.NaN));
Throws<ArgumentOutOfRangeException>(() => new BM25L(docs, delta: -1));
Throws<ArgumentOutOfRangeException>(() => bm.GetBatchScores(new[] {"cat"}, new[] {-1}));
Throws<ArgumentOutOfRangeException>(() => bm.GetBatchScores(new[] {"cat"}, new[] {3}));
Throws<ArgumentOutOfRangeException>(() => bm.GetTopN(new[] {"cat"}, docs, -1));
Throws<ArgumentException>(() => bm.GetTopN(new[] {"cat"}, new[] {"only one"}));
var raw = new BM25Okapi(BM25.Tokenize(new[] {"cat dog", "bird"}, text => text.Split(' ')));
Check(raw.DocumentLengths.SequenceEqual(new[] {2, 1}), "tokenizer delegate");
var source = new[] { new[] {"cat"}, new[] {"bird"} };
var snapshot = new BM25Plus(source);
var before = snapshot.GetScores(new[] {"cat"});
source[0][0] = "dog";
Check(snapshot.GetScores(new[] {"cat"}).SequenceEqual(before), "caller mutation cannot change index");
Throws<ArgumentNullException>(() => new BM25Okapi(null!));
Throws<ArgumentNullException>(() => new BM25Okapi(new string[][] { null! }));
Throws<ArgumentNullException>(() => new BM25Okapi(new[] { new string[] { null! } }));
Throws<ArgumentNullException>(() => bm.GetScores(new string[] { null! }));
Console.WriteLine($"PASS: {cases} Python differential cases; {assertions} assertions including API edge cases; {rankingChecks} distinct-finite ranking comparisons.");
