"""Validate the local image catalog and return its exact distributable inventory."""
import hashlib
import json
from pathlib import Path
import re
import struct


def image_files(root: Path) -> list[Path]:
    directory = root / 'data/images'
    path = directory / 'catalog.json'
    if not path.exists():
        return []
    document = json.loads(path.read_text(encoding='utf-8'))
    entries = document.get('entries')
    if document.get('version') != 1 or not isinstance(entries, list) or len(entries) > 256:
        raise ValueError('Invalid local image catalog')
    files = {path}
    keys = set()
    total = 0
    for entry in entries:
        key = tuple(entry.get(k, '') for k in ('scope', 'kind', 'name', 'width', 'height'))
        digest = entry.get('sha256', '')
        if entry.get('kind') not in ('sprite', 'texture') or not entry.get('name') or key in keys:
            raise ValueError('Invalid or duplicate image target')
        keys.add(key)
        if not re.fullmatch('[0-9a-f]{64}', digest) or entry.get('file') != f'files/{digest}.png':
            raise ValueError('Image payload must use a content-hash path')
        file = directory / entry['file']
        if file.is_symlink() or not 33 <= entry['bytes'] <= 8 * 1024 * 1024 or file.stat().st_size != entry['bytes']:
            raise ValueError('Invalid image payload size or link')
        data = file.read_bytes()
        if data[:8] != b'\x89PNG\r\n\x1a\n' or data[12:16] != b'IHDR' or hashlib.sha256(data).hexdigest() != digest:
            raise ValueError('Image PNG signature or hash mismatch')
        dimensions = struct.unpack('>II', data[16:24])
        if dimensions != (entry['width'], entry['height']) or not all(1 <= n <= 4096 for n in dimensions):
            raise ValueError('Image canvas does not match the native target')
        total += len(data)
        files.add(file)
    if total > 64 * 1024 * 1024:
        raise ValueError('Local images exceed 64 MiB')
    return sorted(files)


if __name__ == '__main__':
    result = image_files(Path(__file__).resolve().parents[1])
    print(f'Validated {len(result)} local image files.')
