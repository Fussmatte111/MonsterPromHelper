"""Dump fields for a type from Assembly-CSharp.dll."""

import sys
import dnfile
from pathlib import Path

TARGETS = sys.argv[1:] or ["StatsManager", "StatBoxManager", "LobbyPlayer"]


def main() -> None:
    path = Path(
        r"C:\Program Files (x86)\Steam\steamapps\common\Monster Prom\MonsterProm_Data\Managed\Assembly-CSharp.dll"
    )
    pe = dnfile.dnPE(path)
    type_by_name = {}
    for row in pe.net.mdtables.TypeDef:
        if row is None:
            continue
        type_by_name[str(row.TypeName)] = row

    for target in TARGETS:
        row = type_by_name.get(target)
        if not row:
            print(f"--- {target}: NOT FOUND ---")
            continue
        print(f"--- {target} ---")
        for f in row.Fields:
            if f is None:
                continue
            print(f"  {f.Name} : {f.Type}")


if __name__ == "__main__":
    main()
