"""Enforce line coverage for the governed changed-feature classes in a Cobertura report."""
import argparse
import json
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def check(report, classes, threshold):
    tree = ET.parse(report)
    observed = {}
    for item in tree.findall(".//class"):
        name = item.attrib["name"]
        owner = name.split('/')[0]
        if owner not in classes:
            continue
        lines = observed.setdefault(owner, {})
        for line in item.findall("./lines/line"):
            key = (item.attrib.get("filename", ""), line.attrib["number"])
            lines[key] = lines.get(key, False) or int(line.attrib["hits"]) > 0
    observed = {name: (sum(lines.values()), len(lines)) for name, lines in observed.items()}
    missing = set(classes) - observed.keys()
    if missing:
        raise ValueError("Coverage report is missing governed classes: " + ", ".join(sorted(missing)))
    failures = []
    for name, (covered, total) in observed.items():
        percentage = covered / total * 100 if total else 100
        if percentage < threshold:
            failures.append(f"{name}: {percentage:.2f}% < {threshold}%")
    if failures:
        raise ValueError("Changed-feature coverage failed: " + "; ".join(failures))
    return observed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path)
    args = parser.parse_args()
    policy = json.loads((ROOT / "scripts/feature-coverage.json").read_text(encoding='utf-8'))
    try:
        result = check(args.report, policy["classes"], policy["threshold"])
    except ValueError as error:
        raise SystemExit(str(error)) from error
    print(f"Changed-feature line coverage passed for {len(result)} classes at {policy['threshold']}%.")


if __name__ == "__main__":
    main()
