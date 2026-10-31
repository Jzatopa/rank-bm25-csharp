using RankBm25;

string[] documents = [
    "C# applications can search local documents without a Python service",
    "BM25 ranks documents using term frequency and document length",
    "Python is convenient for data science and interactive experiments"
];
static string[] Tokens(string text) => text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
var index = new BM25Okapi(BM25.Tokenize(documents, Tokens));
var query = Tokens(args.Length > 0 ? string.Join(" ", args) : "local search");
var scores = index.GetScores(query);
foreach (var document in index.GetTopN(query, documents, 3))
    Console.WriteLine($"{scores[Array.IndexOf(documents, document)]:F6}  {document}");
