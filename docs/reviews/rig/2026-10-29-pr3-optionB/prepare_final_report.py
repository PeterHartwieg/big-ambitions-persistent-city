"""Complete the report from existing measurements and verified cleanup receipts."""
from pathlib import Path
import json
s=Path(__file__).parent
p=s/'report-complete.md'
t=p.read_text(encoding='utf-8')
t=t.replace('Before `7357', '**Recovery completion.** The measurement session stopped before cleanup. This continuation restored the isolated rig and released its lease, completed analysis of the retained dwell/reload trace, and assembled this report. No measurement was rerun and the game was never launched during cleanup.\n\nBefore `7357',1)
t=t.replace('8/8 attached-setting cases pass | PASS bounded settings |','Eight attached settings recorded; numeric text `"75"` is accepted as 75 | PASS clamps/fractional/nonnumeric/missing fallback; numeric-string exception below |')
t=t.replace('The errors concern nullable `SortedSet.Min` results.', 'Exact diagnostics: `Core/OptionalStopScheduling.cs(212,13)` and `(225,48)` raise `CS8602` (possibly null dereference); `(234,131)` and `(235,55)` raise `CS8604` (possibly null `holder` passed to `CitySimulator.Drop`). These concern nullable `SortedSet.Min` results.')
t=t.replace('Numeric text `"75"` is accepted as 75 by the existing converter; nonnumeric text and fractional numbers fall back to 25.', 'Numeric text `"75"` is accepted as 75 by the existing converter; nonnumeric text and fractional numbers fall back to 25. This is an exception to the prompt\'s blanket string-to-25 expectation, and is not claimed as proof that every string falls back. The config summary\'s 8/8 uses the recorded case-specific expectations.')
t=t.replace('**Open issues / remaining limits:** failed strict offline compilation;', '**Open issues / remaining limits:** failed strict offline compilation; numeric-string acceptance differs from the prompt\'s blanket string fallback;')
t=t.replace('](optional-reload-summary.json).\n- hand-on', '](optional-reload-summary.json).\n\n- hand-on')
rest=json.loads((s/'restore-receipt.json').read_text())
target=rest['targeted_verification']
t=t.replace('The campaign helper/fixtures are removed from the active profile by full restoration.', f"The config hash is `{target['persistent_city_config']['sha256']}`; {target['modsLocal_files_verified']} ModsLocal files match. Original autosave was enabled (`preventAutoSave=false`, five minutes, three retained saves). Restoration is established by the complete original profile and preference hashes; no post-restore game launch was used. The campaign helper/fixtures are removed from the active profile by full restoration.")
p.write_text(t,encoding='utf-8',newline='\n')
Path(r'<user-home>\Coding_Projects\.agent-logs\persistent-city-pr3-ob-rig.md').write_text(t+'\nPrivate evidence: '+str(s)+'\n',encoding='utf-8',newline='\n')
print('Completed final report:',len(t.encode()),'bytes')
