"""Generate reference results by executing the retained, unmodified Python source."""
import hashlib
import json
import math
from pathlib import Path
import random
import sys
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from rank_bm25 import BM25Okapi, BM25L, BM25Plus

rng = random.Random(20261031)
cases = []
words = ['common', 'rare', 'cat', 'dog', 'C#', 'c#', 'é', 'e\u0301', '東京', '😀', '', 'missing']

def numbers(values):
    return [float(x) if math.isfinite(x) else str(float(x)) for x in values]

corpora = [
    [['Hello', 'there', 'good', 'man!'], ['It', 'is', 'quite', 'windy', 'in', 'London'], ['How', 'is', 'the', 'weather', 'today?']],
    [['common', 'common'], ['common'], ['common', 'rare']],
    [[], ['cat', 'dog'], ['dog']],
    [['cat']],
    [['é', '😀'], ['e\u0301', '東京'], ['C#', 'c#']],
]
for _ in range(100):
    corpus = [[rng.choice(words[:-1]) for _ in range(rng.randrange(0, 24))] for _ in range(rng.randrange(1, 25))]
    if not any(corpus): corpus[0] = ['cat']
    corpora.append(corpus)

with np.errstate(invalid='ignore', divide='ignore'):
    for ci, corpus in enumerate(corpora):
        queries = [[], ['missing'], ['common', 'common'], ['cat', '😀'], [rng.choice(words) for _ in range(6)]]
        if ci == 0: queries[3] = ['windy', 'London']
        for name, cls in [('okapi', BM25Okapi), ('l', BM25L), ('plus', BM25Plus)]:
            params = {'k1': rng.choice([0.5, 1.5, 2.3]), 'b': rng.choice([0.0, 0.5, 0.75, 1.0])}
            if name == 'okapi': params['epsilon'] = rng.choice([0.0, 0.25, 0.7])
            else: params['delta'] = rng.choice([0.0, 0.5, 1.0, 1.7])
            if ci == 0:
                params = {}  # Exercise actual Python constructor defaults.
            index = cls(corpus, **params)
            ids = [len(corpus)-1, 0, len(corpus)-1]
            for qi, query in enumerate(queries):
                scores = index.get_scores(query)
                # Upstream NumPy quicksort does not promise a portable tie order.
                unique = len(set(scores.tolist())) == len(scores) and np.isfinite(scores).all()
                cases.append({'name': f'{ci}-{name}-{qi}', 'variant': name, 'corpus': corpus,
                    'query': query, 'parameters': params, 'ids': ids,
                    'scores': numbers(scores), 'batch': numbers(index.get_batch_scores(query, ids)),
                    'top': np.argsort(scores)[::-1].tolist() if unique else None})

payload = {'sourceCommit': '47aa3ddf8dc1ebeb7ef4e65f2b4536af44594099',
    'sourceSha256': hashlib.sha256((ROOT / 'rank_bm25.py').read_bytes()).hexdigest(),
    'numpyVersion': np.__version__, 'seed': 20261031, 'cases': cases}
output = ROOT / 'tests/RankBm25.Compatibility/fixtures.json'
output.write_text(json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + '\n')
print(f'Generated {len(cases)} differential cases from Python with NumPy {np.__version__}')
