"""Write a readback-only rig flow for an existing genuine FT fixture. Does not run it."""
import argparse
import json
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--fixture',required=True)
parser.add_argument('--business',action='append',default=[],metavar='STREET#NUMBER=EXACT NAME')
parser.add_argument('--mods',type=Path,help='Reviewed JSON array declaring the complete exact DLL set')
parser.add_argument('--game-build',type=Path,help='Reviewed JSON object containing the installed id/sha256 pin')
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
if not args.fixture.startswith('FT'):
    parser.error('Use a genuine disposable FT fixture name')
businesses={}
for value in args.business:
    if '=' not in value:
        parser.error('--business requires native ID=exact observed name')
    key,name=value.split('=',1)
    if not key or not name or key in businesses:
        parser.error('Explicit unique IDs and exact names are required')
    businesses[key]=name
if len(businesses)>8:
    parser.error('At most eight selected businesses')
def op(**kwargs):
    return {'rig':{'op':'e5b_proof',**kwargs}}
steps=[op(action='discover'),{'expect':'result','path':'after.section','equals':'discover'}]
if businesses:
    steps += [op(action='arm',businesses=list(businesses),expected_names=businesses),
              {'expect':'result','path':'after.armed','equals':True}]
    for business in businesses:
        for section in ['business','cargo','orders','history','receipts','residents','statements','transactions']:
            steps.append(op(action='snapshot',id=business,section=section,offset=0,limit=8))
            steps.append({'expect':'result','path':'after.section','equals':section})
    steps += [op(action='status'),op(action='disarm'),{'expect':'result','path':'after.armed','equals':False}]
document={'name':'E5b standalone probe discovery/readback',
          'checks':'Discovery/arm/raw first-page snapshots only; no native behavior or timing acceptance implied',
          'fixture':args.fixture,'steps':steps}
if args.mods:
    document['mods']=json.loads(args.mods.read_text(encoding='utf-8-sig'))
if args.game_build:
    document['game_build']=json.loads(args.game_build.read_text(encoding='utf-8-sig'))
args.output.parent.mkdir(parents=True,exist_ok=True)
with args.output.open('x',encoding='utf-8') as stream:
    json.dump(document,stream,indent=2)
    stream.write('\n')
print(args.output)
