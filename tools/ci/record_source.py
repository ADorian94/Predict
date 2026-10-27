"""Record the resolved checkout SHA, not the user-entered ref or a build constant."""
import json
import os
import re
from pathlib import Path


def record(source_sha, workflow_sha, directory):
    for sha in (source_sha, workflow_sha):
        if not re.fullmatch(r"[0-9a-f]{40}", sha) or sha == "0" * 40:
            raise ValueError("Expected a nonzero full lowercase commit SHA")
    manifest = {
        "source_sha": source_sha,
        "workflow_sha": workflow_sha,
        "artifact_name": "predict-dev-" + source_sha,
        "configuration": "Release",
        "runtime": "android-arm64",
        "test_ads": True,
        "trimmed": True,
        "signing": "runner-debug",
    }
    directory.mkdir(parents=True, exist_ok=True)
    (directory / "source-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return manifest


def main():
    manifest = record(os.environ["SOURCE_SHA"], os.environ["WORKFLOW_SHA"], Path(os.environ["MANIFEST_DIR"]))
    print(json.dumps(manifest, indent=2))
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
        output.write(f"source_sha={manifest['source_sha']}\nartifact_name={manifest['artifact_name']}\n")


if __name__ == "__main__":
    main()
