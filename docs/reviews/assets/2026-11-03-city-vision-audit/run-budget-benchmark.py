#!/usr/bin/env python3
"""Run the isolated audit fixture without touching game data or repo sources."""

from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET


def main():
    here = Path(__file__).resolve().parent
    root = here.parents[3]
    dotnet = shutil.which("dotnet")
    if dotnet is None:
        raise SystemExit("The .NET 10 SDK must be available on PATH.")
    with tempfile.TemporaryDirectory(prefix="pc-city-audit-") as directory:
        scratch = Path(directory)
        project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        properties = ET.SubElement(project, "PropertyGroup")
        for name, value in {"OutputType": "Exe", "TargetFramework": "net10.0",
                            "ImplicitUsings": "enable", "Nullable": "enable"}.items():
            ET.SubElement(properties, name).text = value
        items = ET.SubElement(project, "ItemGroup")
        ET.SubElement(items, "ProjectReference", Include=str(root / "Core/PersistentCity.Core.csproj"))
        ET.ElementTree(project).write(scratch / "Audit.csproj", encoding="unicode")
        shutil.copyfile(here / "budget-posting-benchmark.cs.txt", scratch / "Program.cs")
        subprocess.run([dotnet, "run", "--project", str(scratch / "Audit.csproj"),
                        "-c", "Release", "--verbosity", "quiet"], cwd=root, check=True)


if __name__ == "__main__":
    main()
