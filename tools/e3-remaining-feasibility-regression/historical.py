"""Remove only exact batch additions for historical SHA-256 comparisons.

Old hashes are immutable. The batch source guard pins every complete modified file
and tests that unrelated mutations still fail. No general pattern stripping.
"""
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
EDITS = {'Runtime/DirectStockShowcaseReader.cs': [('        internal static readonly DirectStockCandidate FrenchFries = new '
                                           'DirectStockCandidate("ba:itemname_frenchfries");\n',
                                           '        internal static readonly DirectStockCandidate FrenchFries = new '
                                           'DirectStockCandidate("ba:itemname_frenchfries");\n'
                                           '        internal static readonly DirectStockCandidate CheesePlatter = new '
                                           'DirectStockCandidate("ba:itemname_cheeseplatter");\n'
                                           '        internal static readonly DirectStockCandidate CottonCandy = new '
                                           'DirectStockCandidate("ba:itemname_cottoncandy");\n'
                                           '        internal static readonly DirectStockCandidate Popcorn = new '
                                           'DirectStockCandidate("ba:itemname_popcorn");\n'
                                           '        internal static readonly DirectStockCandidate Kabob = new '
                                           'DirectStockCandidate("ba:itemname_kabob");\n'
                                           '        internal static readonly DirectStockCandidate CandyCane = new '
                                           'DirectStockCandidate("ba:itemname_candycane");\n'
                                           '        internal static readonly DirectStockCandidate ChristmasCookies = '
                                           'new DirectStockCandidate("ba:itemname_christmascookies");\n')],
 'tools/donut-feasibility-regression/source_checks.py': [('import hashlib\n', 'import hashlib\nimport runpy\n'),
                                                         ('ROOT = Path(__file__).resolve().parents[2]\n',
                                                          'ROOT = Path(__file__).resolve().parents[2]\n'
                                                          'batch_source = runpy.run_path(str(ROOT / '
                                                          '"tools/e3-remaining-feasibility-regression/historical.py"))["source"]\n'),
                                                         ('source = (ROOT / path).read_text(encoding="utf-8")',
                                                          'source = batch_source(path)'),
                                                         ('        if path not in readers:\n',
                                                          '        if path not in readers and path != ROOT / '
                                                          '"Runtime/RemainingBatchFeasibilityReaders.cs":\n'),
                                                         ('"FrenchFriesFeasibility", "DirectStockShowcase", '
                                                          '"DirectStockCandidate"):',
                                                          '"FrenchFriesFeasibility", "CheesePlatterFeasibility", '
                                                          '"CottonCandyFeasibility", "PopcornFeasibility", '
                                                          '"KabobFeasibility", "CandyCaneFeasibility", '
                                                          '"ChristmasCookiesFeasibility", '
                                                          '"RemainingSeasonalStockProbe", "DirectStockShowcase", '
                                                          '"DirectStockCandidate"):')],
 'tools/icecream-feasibility-regression/source_checks.py': [('ROOT = Path(__file__).resolve().parents[2]\n',
                                                             'ROOT = Path(__file__).resolve().parents[2]\n'
                                                             'batch_source = runpy.run_path(str(ROOT / '
                                                             '"tools/e3-remaining-feasibility-regression/historical.py"))["source"]\n'),
                                                            ('source = (ROOT / path).read_text(encoding="utf-8")',
                                                             'source = batch_source(path)'),
                                                            ('for directory in ("Core", "Runtime", "Persistence", '
                                                             '"Compatibility"):\n',
                                                             'allowed.add(ROOT / '
                                                             '"Runtime/RemainingBatchFeasibilityReaders.cs")\n'
                                                             'for directory in ("Core", "Runtime", "Persistence", '
                                                             '"Compatibility"):\n')],
 'tools/pizza-feasibility-regression/source_checks.py': [('ROOT = Path(__file__).resolve().parents[2]\n',
                                                          'ROOT = Path(__file__).resolve().parents[2]\n'
                                                          'batch_source = runpy.run_path(str(ROOT / '
                                                          '"tools/e3-remaining-feasibility-regression/historical.py"))["source"]\n'),
                                                         ('(ROOT / path).read_bytes().replace(b"\\r\\n", b"\\n")',
                                                          'batch_source(path).encode()')],
 'tools/frenchfries-feasibility-regression/source_checks.py': [('ROOT = Path(__file__).resolve().parents[2]\n',
                                                                'ROOT = Path(__file__).resolve().parents[2]\n'
                                                                'batch_source = runpy.run_path(str(ROOT / '
                                                                '"tools/e3-remaining-feasibility-regression/historical.py"))["source"]\n'),
                                                               ('(ROOT / path).read_text(encoding="utf-8").encode()',
                                                                'batch_source(path).encode()'),
                                                               ('# Recursively cover future nested runtime/core '
                                                                "sources as well as today's flat tree.\n",
                                                                'readers.add(ROOT / '
                                                                '"Runtime/RemainingBatchFeasibilityReaders.cs")\n'
                                                                '# Recursively cover future nested runtime/core '
                                                                "sources as well as today's flat tree.\n")],
 'tools/salad-feasibility-regression/compare_baseline.py': [('import hashlib\n', 'import hashlib\nimport runpy\n'),
                                                            ('ROOT = Path(__file__).resolve().parents[2]\n',
                                                             'ROOT = Path(__file__).resolve().parents[2]\n'
                                                             'batch_source = runpy.run_path(str(ROOT / '
                                                             '"tools/e3-remaining-feasibility-regression/historical.py"))["source"]\n'),
                                                            ('body = (ROOT / path).read_text(encoding="utf-8")',
                                                             'body = batch_source(path)')],
 'tools/icecream-feasibility-regression/measure_baseline.py': [('import hashlib\n', 'import hashlib\nimport runpy\n'),
                                                               ('ROOT = Path(__file__).resolve().parents[2]\n',
                                                                'ROOT = Path(__file__).resolve().parents[2]\n'
                                                                'batch_source = runpy.run_path(str(ROOT / '
                                                                '"tools/e3-remaining-feasibility-regression/historical.py"))["source"]\n'),
                                                               ('shared = (ROOT / '
                                                                '"Runtime/DirectStockShowcaseReader.cs").read_text(encoding="utf-8")',
                                                                'shared = '
                                                                'batch_source("Runtime/DirectStockShowcaseReader.cs")')],
 'tools/frenchfries-feasibility-regression/measure_before.py': [('import hashlib\n', 'import hashlib\nimport runpy\n'),
                                                                ('ROOT = Path(__file__).resolve().parents[2]\n',
                                                                 'ROOT = Path(__file__).resolve().parents[2]\n'
                                                                 'batch_source = runpy.run_path(str(ROOT / '
                                                                 '"tools/e3-remaining-feasibility-regression/historical.py"))["source"]\n'),
                                                                ('source = (ROOT / path).read_text(encoding="utf-8")',
                                                                 'source = batch_source(path)')]}

def source(path):
    text = (ROOT / path).read_text(encoding="utf-8")
    for before, after in reversed(EDITS.get(path, [])):
        assert text.count(after) == 1, "Exact batch addition changed: " + path
        text = text.replace(after, before, 1)
    return text
