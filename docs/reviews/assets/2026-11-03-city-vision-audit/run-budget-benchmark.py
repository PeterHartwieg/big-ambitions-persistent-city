#!/usr/bin/env python3
"""Build and run a temporary copy of the audit fixture and Core project."""

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
        shutil.copytree(root / "Core", scratch / "Core",
                        ignore=shutil.ignore_patterns("bin", "obj"))
        fixture = scratch / "Fixture"
        fixture.mkdir()
        project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        properties = ET.SubElement(project, "PropertyGroup")
        for name, value in {"OutputType": "Exe", "TargetFramework": "net10.0",
                            "ImplicitUsings": "enable", "Nullable": "enable"}.items():
            ET.SubElement(properties, name).text = value
        items = ET.SubElement(project, "ItemGroup")
        ET.SubElement(items, "ProjectReference", Include=str(scratch / "Core/PersistentCity.Core.csproj"))
        ET.ElementTree(project).write(fixture / "Audit.csproj", encoding="unicode")
        shutil.copyfile(here / "budget-posting-benchmark.cs.txt", fixture / "Program.cs")
        subprocess.run([dotnet, "run", "--project", str(fixture / "Audit.csproj"),
                        "-c", "Release", "--verbosity", "quiet"], cwd=scratch, check=True)


if __name__ == "__main__":
    main()
