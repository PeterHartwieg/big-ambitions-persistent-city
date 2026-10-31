from pathlib import Path
import sys,hashlib,json,os
s=Path(__file__).parent
root=Path(sys.argv[1]).resolve();mode=sys.argv[2]
script=(root/'tools/build.py').read_text()
script=script.replace('root = Path(__file__).resolve().parents[1]','root = Path('+repr(str(root))+')')
script=script.replace('output = root / "build/PersistentCity"','output = Path('+repr(str(s/'packages'))+') / '+repr(mode)+' / "PersistentCity"')
script=script.replace('archive = root / "build/lib.harmony.2.4.2.nupkg"','archive = Path('+repr(str(s/'lib.harmony.2.4.2.nupkg'))+')')
sys.argv=[str(root/'tools/build.py'),'--game-managed',os.environ.get('BA_GAME_MANAGED') or 'C:/BigAmbitions-AgentTests/Big Ambitions Agent Tests_Data/Managed']
exec(compile(script,str(root/'tools/build.py'),'exec'),{'__file__':str(root/'tools/build.py'),'__name__':'__main__'})
