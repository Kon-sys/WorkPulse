"""Render documentation from schema-metadata.json exported from PostgreSQL."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent
metadata = json.loads((root / 'schema-metadata.json').read_text(encoding='utf-8-sig'))
tables = sorted({c['table_name'] for c in metadata['columns']})
lines = ['# Физический словарь данных', '',
         'Автоматический снимок PostgreSQL после V007; источник структуры — SQL-миграции.', '',
         f"Бизнес-таблиц: {metadata['tables']}; с включённым и принудительным RLS: {metadata['rls_tables']}.", '']
diagram = ['# Физическая ER-диаграмма', '',
           'Сгенерирована из фактических столбцов и FK PostgreSQL. Составные ключи и CHECK полностью перечислены в словаре.', '',
           '```mermaid', 'erDiagram']
for table in tables:
    constraints = [c for c in metadata['constraints'] if c['table_name'] == 'workpulse.' + table]
    primary = next((c['definition'] for c in constraints if c['definition'].startswith('PRIMARY KEY')), '')
    primary_fields = set(re.findall(r'\b[a-z_]+\b', primary.split('(', 1)[-1]))
    lines += [f'## {table}', '', '| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |', '|---|---|---|---|']
    diagram += [f'    {table} {{']
    for c in (c for c in metadata['columns'] if c['table_name'] == table):
        default = (c['column_default'] or '—').replace('|', '\\|')
        lines.append(f"| `{c['column_name']}` | `{c['data_type']}` | {c['is_nullable']} | `{default}` |")
        key = ' PK' if c['column_name'] in primary_fields else ''
        diagram.append(f"        {c['udt_name']} {c['column_name']}{key}")
    lines += ['', 'Ограничения:', '']
    for c in constraints:
        lines.append(f"- `{c['conname']}`: `{c['definition']}`")
    lines += ['']
    diagram += ['    }']
edges = set()
for c in metadata['constraints']:
    match = re.search(r'FOREIGN KEY \(([^)]+)\) REFERENCES (?:workpulse\.)?([a-z_]+)\(', c['definition'])
    if match:
        child = c['table_name'].split('.')[-1]
        fields, parent = match.groups()
        columns = [v.strip() for v in fields.split(',')]
        optional = any(col['is_nullable'] == 'YES' for col in metadata['columns']
                       if col['table_name'] == child and col['column_name'] in columns)
        edge = (parent, child, fields, optional)
        edges.add(edge)
for parent, child, fields, optional in sorted(edges):
    # Parent cardinality depends on nullable FK; child side is generally many.
    parent_side = '|o' if optional else '||'
    diagram.append(f'    {parent} {parent_side}--o{{ {child} : "{fields}"')
diagram += ['```', '', 'Отдельные UNIQUE (например, сотрудник ↔ учётная запись и один обед) уточняют общую кратность FK; см. словарь.']
(root / 'data-dictionary.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
(root / 'er-diagram.md').write_text('\n'.join(diagram) + '\n', encoding='utf-8')
print(f"Generated documentation: {len(tables)} tables, {len(edges)} FK relationships")
