"""Record the resolved checkout SHA, not the user-entered ref or a build constant."""
import argparse
import json
import os
import re
from pathlib import Path
import xml.etree.ElementTree as ET

TOOLCHAIN = {"target_framework": "net10.0-android36.0", "sdk": "10.0.403",
             "workload_set": "10.0.403", "maui_version": "10.0.120"}

def verify_source(directory):
    project = ET.parse(directory / "Predict.csproj")
    pin = json.loads((directory / "global.json").read_text(encoding="utf-8"))
    expected = {"TargetFrameworks": TOOLCHAIN["target_framework"], "MauiVersion": TOOLCHAIN["maui_version"],
                "RuntimeIdentifier": "android-arm64", "SupportedOSPlatformVersion": "26.0"}
    for name, value in expected.items():
        if project.findtext(".//" + name) != value:
            raise ValueError("Unexpected source property: " + name)
    if pin.get("sdk") != {"version": TOOLCHAIN["sdk"], "rollForward": "disable", "allowPrerelease": False, "workloadVersion": TOOLCHAIN["workload_set"]}:
        raise ValueError("Unexpected source SDK/workload pin")
    return dict(TOOLCHAIN)



def record(source_sha, workflow_sha, directory, artifact_kind="test-apk"):
    if artifact_kind not in ("test-apk", "unsigned-production-aab"):
        raise ValueError("Unknown artifact kind")
    production = artifact_kind == "unsigned-production-aab"
    for sha in (source_sha, workflow_sha):
        if not re.fullmatch(r"[0-9a-f]{40}", sha) or sha == "0" * 40:
            raise ValueError("Expected a nonzero full lowercase commit SHA")
    manifest = {
        **TOOLCHAIN,
        "source_sha": source_sha,
        "workflow_sha": workflow_sha,
        "artifact_kind": artifact_kind,
        "artifact_name": ("predict-unsigned-production-" if production else "predict-dev-") + source_sha,
        "build_profile": "Production" if production else "Dev",
        "configuration": "Release",
        "runtime": "android-arm64",
        "test_ads": not production,
        "trimmed": True,
        "signing": "unsigned" if production else "runner-debug",
    }
    directory.mkdir(parents=True, exist_ok=True)
    (directory / "source-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify-source", type=Path)
    args = parser.parse_args()
    if args.verify_source is not None:
        print(json.dumps(verify_source(args.verify_source), indent=2))
        return
    verify_source(Path(os.environ["SOURCE_DIR"]))
    manifest = record(os.environ["SOURCE_SHA"], os.environ["WORKFLOW_SHA"], Path(os.environ["MANIFEST_DIR"]))
    print(json.dumps(manifest, indent=2))
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
        output.write(f"source_sha={manifest['source_sha']}\nartifact_name={manifest['artifact_name']}\n")


if __name__ == "__main__":
    main()
