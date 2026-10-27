"""Run every executable regression project and all Python tests, failing fast."""
import argparse
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET


def commands(root, dotnet="dotnet"):
    result = []
    for project in sorted((root / "tests").rglob("*.csproj")):
        if not any(node.text == "Exe" for node in ET.parse(project).iter("OutputType")):
            continue
        relative = project.relative_to(root).as_posix()
        base = [dotnet, "run", "--project", relative, "-c", "Release"]
        if project.name == "Predict.BannerTests.csproj":
            result.extend([base + [f"-p:PredictTestAds={value}"] for value in ("true", "false")])
        else:
            result.append(base)
            if project.name == "Predict.AdViewsTests.csproj":
                result.extend([base + ["--", flag] for flag in ("--unload-repro", "--detach-during-remove")])
    if not result:
        raise ValueError("No executable regression projects found")
    result.append([sys.executable, "-m", "unittest", "discover", "-s", "tests", "-p", "test_*.py", "-v"])
    return result


def run_commands(plan, root):
    for command in plan:
        print(subprocess.list2cmdline(command), flush=True)
        subprocess.run(command, cwd=root, check=True, timeout=300)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--dotnet", default="dotnet")
    args = parser.parse_args()
    run_commands(commands(args.root, args.dotnet), args.root)


if __name__ == "__main__":
    main()
