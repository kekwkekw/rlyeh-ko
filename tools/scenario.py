"""Prepare local UTAGE review files; never translates, downloads, or edits game books."""
import argparse
import gzip
import json
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def draft(book: Path) -> dict:
    data = gzip.decompress(book.read_bytes()) if book.suffix == '.gz' else book.read_bytes()
    tree = json.loads(data)
    entries = []
    for grid in tree['importGridList']:
        columns = None
        header = -1
        for index, row in enumerate(grid['rows']):
            cells = row['strings']
            if 'Text' in cells and 'Command' in cells:
                columns, header = cells, index
                continue
            if columns is None or row.get('isEmpty') or row.get('isCommentOut'):
                continue
            text_column = columns.index('Text')
            if len(cells) <= text_column or not cells[text_column]:
                continue
            if cells[columns.index('Command')].lstrip().startswith('//'):
                continue
            entries.append(dict(id=f"{tree['m_Name']}:{grid['name']}:{index}", grid=grid['name'],
                row=index, serializedRow=row['rowIndex'], headerRow=header, textColumn=text_column,
                columns=columns, sourceCells=cells, target='', reviewer=''))
    return dict(version=1, entries=entries)


def compile_reviews(paths: list[Path], output: Path) -> None:
    entries = []
    for path in paths:
        doc = json.loads(path.read_text(encoding='utf-8-sig'))
        if doc.get('version') != 1:
            raise ValueError(f'Unsupported review file: {path}')
        entries.extend(e for e in doc['entries'] if e.get('target', '').strip() and e.get('reviewer', '').strip())
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='rlyeh-scenario-') as temporary:
        staged = Path(temporary) / 'reviewed.json'
        staged.write_text(json.dumps(dict(version=1, entries=entries), ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
        # Use the runtime's validator, including exact tags/variables and row identity.
        subprocess.run(['dotnet', 'run', '--project', str(ROOT/'tests/CoreChecks'), '-c', 'Release',
                        '--no-restore', '--', '--scenario-catalog', str(staged)], cwd=ROOT, check=True)
        output.write_bytes(staged.read_bytes())
    print(f'Compiled {len(entries)} reviewed rows; pending rows use hdTrans.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    export = commands.add_parser('draft')
    export.add_argument('book', type=Path, help='Extracted importGridList tree JSON or JSON.gz')
    export.add_argument('--output', type=Path, required=True)
    build = commands.add_parser('compile')
    build.add_argument('reviews', type=Path, nargs='+')
    build.add_argument('--output', type=Path, default=ROOT/'data/scenario/reviewed.json')
    args = parser.parse_args()
    if args.command == 'draft':
        doc = draft(args.book)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(doc, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
        print(f"Drafted {len(doc['entries'])} source rows; target/reviewer are empty.")
    else:
        compile_reviews(args.reviews, args.output)


if __name__ == '__main__':
    main()
