"""Prepare tracked-source baseline and existing native doubles; no game/rig input."""
from pathlib import Path
import subprocess
import io
import tarfile

root = Path(__file__).resolve().parents[2]
out = root / "build/e4-menu"
out.mkdir(parents=True, exist_ok=True)
archive_bytes=subprocess.check_output(["git","archive","51ce709","Core","Persistence","Runtime","tools/save-contract-regression"],cwd=root)
baseline_root=root/"build/e4-baseline"
baseline_root.mkdir(exist_ok=True)
with tarfile.open(fileobj=io.BytesIO(archive_bytes)) as archive:
    archive.extractall(baseline_root,filter="data")
baseline = subprocess.check_output(["git", "show", "51ce709:Runtime/FoodSupplyReader.cs"], cwd=root, text=True)
(out / "FoodSupplyReaderBefore.cs").write_text(baseline.replace("class FoodSupplyReader", "class FoodSupplyReaderBefore").replace("FoodSupplySnapshot", "FoodSupplySnapshotBefore"))
choice = subprocess.check_output(["git", "show", "51ce709:Core/FoodChoicePolicy.cs"], cwd=root, text=True)
choice = choice[choice.index("    public static partial class FoodChoicePolicy"):]
choice = choice.replace("class FoodChoicePolicy", "class FoodChoicePolicyBefore", 1)
(out / "FoodChoicePolicyBefore.cs").write_text("using System;\nusing System.Collections.Generic;\nnamespace PersistentCity.Core\n{\n" + choice)
doubles = (root / "tools/food-supply-regression/Program.cs").read_text()
(out / "Doubles.cs").write_text("using System;\nusing System.Collections.Generic;\nusing BigAmbitions.Items;\nusing Buildings.Retail.Simulation;\nusing UnityEngine;\n" + doubles[doubles.index("public sealed class Save"):])
print("Prepared baseline 51ce709 and tracked supply doubles under build/e4-menu.")
