"""rig_test: prepare isolated, private staging; never copy private backups to Git."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[4]
STAGE = Path.home() / 'HART-Builds/persistent-city-perf-review-2026-10-30-r2'
FIRST_STAGE = Path.home() / 'HART-Builds/persistent-city-perf-review-2026-10-30'
PROFILE = Path.home() / 'AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests'
OWNER = 'persistent-city-perf-review-rig_test'

def main():
    STAGE.mkdir(exist_ok=False)
    old = ROOT / 'docs/reviews/rig/2026-10-29-pr3-optionB-r2'
    for name in ['Wait-Lease.ps1', 'Backup-Rig.ps1', 'Restore-Rig.ps1', 'Release.ps1', 'Launch-Rig.ps1', 'rig.py']:
        text = (old / name).read_text(encoding='utf-8-sig')
        text = text.replace('<private-evidence>', str(STAGE)).replace('<agent1-profile>', str(PROFILE)).replace('<user-home>', str(Path.home()))
        text = text.replace('persistent-city-pr3-ob5-rig', OWNER)
        text = text.replace('PR3 isolated before/after strict financial migration checkout QA', 'measurement-only current branch performance review')
        if name == 'Wait-Lease.ps1':
            text = text.replace('while($true){', "$started=[DateTime]::UtcNow\nwhile($true){\n if(([DateTime]::UtcNow-$started).TotalHours -ge 3){throw 'BLOCKED: lease unavailable for three hours'}")
        if name == 'rig.py':
            text = text.replace("guard=guard or op in ['status'] or op.startswith('optional-') or op=='codec';", 'guard=guard;')
            text = text.replace("SHOTS=pathlib.Path(r'" + str(Path.home()) + r'\HART-Builds\screenshots\persistent-city-pr3-ob5-2026-10-29' + "')", "SHOTS=STAGE/'screenshots'")
        (STAGE / name).write_text(text, encoding='utf-8')
    subprocess.run(['powershell', '-NoProfile', '-File', str(STAGE/'Wait-Lease.ps1')], check=True)
    subprocess.run(['powershell', '-NoProfile', '-File', str(STAGE/'Backup-Rig.ps1')], check=True)

if __name__ == '__main__':
    main()
