"""Record exact executable test methods for acceptance IDs; review assertion quality separately."""
import argparse
import json
import runpy
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    policy = runpy.run_path(str(ROOT / 'scripts/validate-repository.py'))
    content = json.dumps(policy['acceptance_map'](), indent=2) + '\n'
    path = ROOT / 'scripts/feature-acceptance-map.json'
    if args.check:
        if not path.exists() or path.read_text(encoding='utf-8') != content:
            raise SystemExit('Executable acceptance map is stale.')
    else:
        path.write_text(content, newline='\n')
    print(f"Exact executable methods mapped for {len(json.loads(content))} acceptance IDs.")


if __name__ == '__main__':
    main()
