"""Validate source organization, acceptance traceability, documentation and workflow policy."""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TYPE = re.compile(r"\b(?:(?:class|interface|enum|struct|record(?:\s+(?:class|struct))?)\s+(\w+)|delegate\s+\w+(?:<[^>]+>)?\s+(\w+))")
CASE = re.compile(r"^\|\s*([A-Z]+-\d+)\s*\|\s*(Success|Failure|Race|Recovery|Characterization)\s*\|", re.M)
SCENARIO = re.compile(r"^###\s+([A-Z]+-\d+):", re.M)


def acceptance_map(root=ROOT):
    result = {}
    declaration = re.compile(r'((?:\s*\[[^\n]+\]\s*)+)public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\s*\(')
    for path in (root / "tests").rglob("*.cs"):
        if any(x in path.parts for x in ("bin", "obj")):
            continue
        text = path.read_text(encoding="utf-8-sig")
        for match in declaration.finditer(text):
            cases = re.findall(r'\[Trait\("Spec",\s*"([A-Z]+-\d+)"\)\]', match[1])
            for case in cases:
                result.setdefault(case, []).append({
                    "reference": path.relative_to(root).as_posix() + "#" + match[2],
                    "assertions": match[2].replace('_', ' ')
                })
    return {case: sorted(refs, key=lambda x: x["reference"]) for case, refs in sorted(result.items())}


def source_types(text):
    # Remove comments and string literals before finding top-level declarations.
    text = re.sub(r'//[^\n]*|/\*.*?\*/|@?"(?:""|\\.|[^"\\])*"|\x27(?:\\.|[^\x27\\])*\x27', '', text, flags=re.S)
    depth = 0
    top = []
    tokens = re.compile(r"[{}]|" + TYPE.pattern)
    for token in tokens.finditer(text):
        if token.group() == "{":
            depth += 1
        elif token.group() == "}":
            depth -= 1
        elif depth == 0:
            top.append(token.group(1) or token.group(2))
    return top


def validate(root=ROOT):
    errors = []
    actual_map = acceptance_map(root)
    expected_map = json.loads((root / "scripts/feature-acceptance-map.json").read_text(encoding='utf-8'))
    if actual_map != expected_map:
        errors.append("Executable acceptance mapping drift: run scripts/generate-acceptance-map.py and review behavioral assertions.")
    baseline = json.loads((root / "scripts/source-policy-baseline.json").read_text(encoding='utf-8'))
    observed = {}
    for folder in ("src", "tests"):
        for path in (root / folder).rglob("*.cs"):
            if any(x in path.parts for x in ("obj", "bin", "Migrations")) or path.name == "Program.cs" or path.name.endswith(".generated.cs"):
                continue
            names = source_types(path.read_text(encoding="utf-8-sig"))
            if len(names) != 1 or names[0] != path.stem:
                observed[path.relative_to(root).as_posix()] = names
    for path, names in observed.items():
        if baseline.get(path) != names:
            errors.append(f"Source organization violation: {path}: {names}")
    for path in baseline:
        if path not in observed:
            errors.append(f"Remove resolved source-policy baseline entry: {path}")

    mapped = set()
    for path in (root / "tests").rglob("*.cs"):
        if any(x in path.parts for x in ("bin", "obj")):
            continue
        mapped.update(re.findall(r'\[Trait\("Spec",\s*"([A-Z]+-\d+)"\)\]', path.read_text(encoding="utf-8-sig")))
    evidence = json.loads((root / "scripts/acceptance-evidence.json").read_text(encoding='utf-8'))
    index = (root / "specs/README.md").read_text(encoding='utf-8')
    indexed = dict(re.findall(r"\| \[[^\]]+\]\(([^)]+/spec\.md)\) \| (\w+) \|", index))
    owners = {}
    for path in (root / "specs").glob("*/spec.md"):
        if path.parent.name == "_template":
            continue
        text = path.read_text(encoding="utf-8-sig")
        status = re.search(r"^- Status: (\w+)", text, re.M)
        relative = path.relative_to(root / "specs").as_posix()
        if not status or indexed.get(relative) != status[1]:
            errors.append(f"Specification owner/status index mismatch: {relative}")
        for case, kind in CASE.findall(text):
            owners[case] = path
            if kind != "Characterization" and case not in mapped and case not in evidence:
                errors.append(f"Unmapped acceptance case: {case}")
        for case in SCENARIO.findall(text):
            owners[case] = path
            if case not in mapped and case not in evidence:
                errors.append(f"Unmapped completion scenario: {case}")
            entry = evidence.get(case, {})
            if status and status[1] == "Implemented" and entry.get("disposition") == "pending":
                errors.append(f"Implemented specification has pending evidence: {case}")
    for case, entry in evidence.items():
        if case not in owners:
            errors.append(f"Evidence has no current owner: {case}")
        if not entry.get("assertions"):
            errors.append(f"Evidence has no behavioral assertions: {case}")
        for ref in entry.get("references", []):
            if not (root / ref).is_file():
                errors.append(f"Missing evidence reference: {case}: {ref}")
        if entry.get("disposition") == "pending" and not entry.get("reason"):
            errors.append(f"Pending evidence has no explicit reason: {case}")

    for path in list((root / "specs").rglob("*.md")) + list((root / "docs").glob("*.md")) + [root / "README.md", root / "CONTRIBUTING.md"]:
        for target in re.findall(r"\]\(([^)]+)\)", path.read_text(encoding="utf-8-sig")):
            if "://" in target or target.startswith("#"):
                continue
            target = target.split("#")[0].split(":")[0]
            if target and not (path.parent / target).exists():
                errors.append(f"Broken documentation link: {path.relative_to(root)}: {target}")
    for path in (root / ".github/workflows").glob("*.yml"):
        text = path.read_text(encoding='utf-8')
        for action in re.findall(r"uses:\s*(\S+)", text):
            if not action.startswith("./") and not re.fullmatch(r"[^@]+@[a-f0-9]{40}", action):
                errors.append(f"Unpinned action: {path.name}: {action}")
        if "pull_request_target:" in text or "write-all" in text:
            errors.append(f"Unsafe workflow permission/trigger: {path.name}")
        if "pull_request:" in text and "secrets.PRESENTATION_" in text:
            errors.append(f"Production secrets in PR workflow: {path.name}")
    return errors


def main():
    argparse.ArgumentParser(description=__doc__).parse_args()
    errors = validate()
    if errors:
        raise SystemExit("\n".join(errors))
    print("Source policy, acceptance mappings, documentation links and workflow hardening passed.")


if __name__ == "__main__":
    main()
