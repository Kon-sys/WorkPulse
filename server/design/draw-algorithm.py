"""Editable A4 draft algorithm drawing; standard-library-only SVG generator."""
from pathlib import Path
from html import escape

out = Path(__file__).parent / 'violation-drawing.svg'
svg = ['<svg xmlns="http://www.w3.org/2000/svg" width="210mm" height="297mm" viewBox="0 0 210 297">',
       '<rect width="210" height="297" fill="white"/>',
       '<defs><marker id="arrow" markerWidth="4" markerHeight="4" refX="3.5" refY="2" orient="auto"><path d="M0,0 L4,2 L0,4 Z" fill="black"/></marker></defs>',
       '<g font-family="Arial, sans-serif" fill="black" font-size="3.2">',
       '<rect x="20" y="5" width="185" height="287" fill="none" stroke="black" stroke-width="0.5"/>']

def text(x, y, value, size=3.2, anchor='middle'):
    svg.append(f'<text x="{x}" y="{y}" font-size="{size}" text-anchor="{anchor}">{escape(value)}</text>')

for x in [20, 80, 140, 200]:
    svg.append(f'<path d="M{x},20 V220" stroke="#ddd" stroke-width="0.2"/>')
for row in range(9):
    y = 20 + 25 * row
    svg.append(f'<path d="M20,{y} H200" stroke="#ddd" stroke-width="0.2"/>')
for col, x in zip('ABC', [50, 110, 170]): text(x, 17, col, 2.8)
for row in range(8): text(23, 24 + 25 * row, str(row + 1), 2.5)

def node(col, row, lines, shape='rect'):
    x = 20 + col * 60; y = 20 + row * 25
    attrs = 'fill="white" stroke="black" stroke-width="0.35"'
    if shape == 'diamond':
        svg.append(f'<path d="M{x+30},{y} L{x+60},{y+12.5} L{x+30},{y+25} L{x},{y+12.5} Z" {attrs}/>')
    elif shape == 'terminal':
        svg.append(f'<rect x="{x}" y="{y}" width="60" height="25" rx="12.5" {attrs}/>')
    else:
        svg.append(f'<rect x="{x}" y="{y}" width="60" height="25" {attrs}/>')
    label=f'{"ABC"[col]}{row+1}'
    # Coordinate label inset 5 mm from the left boundary; white patch interrupts the contour.
    svg.append(f'<rect x="{x+4.5}" y="{y-1.5}" width="8" height="3.5" fill="white"/>')
    text(x+5, y+1, label, 2.5, 'start')
    first=y+12.5-(len(lines)-1)*2
    for index, line in enumerate(lines): text(x+30, first+index*4, line, 3)

def path(d): svg.append(f'<path d="{d}" fill="none" stroke="black" stroke-width="0.35" marker-end="url(#arrow)"/>')

node(1, 0, ['Начало Detect'], 'terminal')
node(1, 1, ['Вход: интервалы, политика;', 'нарушения := []'])
node(1, 2, ['Остался', 'интервал?'], 'diamond')
node(0, 2, ['Вернуть нарушения;', 'конец'], 'terminal')
node(1, 3, ['Следующий интервал;', 'выбрать правило и лимит'])
node(1, 4, ['Есть', 'правило?'], 'diamond')
node(1, 5, ['Длительность', '> лимит?'], 'diamond')
node(1, 6, ['Добавить нарушение:', 'тип, начало, конец, лимит'])
# Adjacent cells touch by design; bridge across their contours with short direction marks.
for y in [45,70,95,120,145,170]: path(f'M110,{y-2} V{y+2}')
path('M80,82.5 H77'); text(73,79,'нет',2.6)
path('M140,132.5 H202 V82.5 H140'); text(155,130,'нет',2.6)
path('M140,157.5 H202'); text(155,155,'нет',2.6)
path('M110,195 V210 H202 V82.5 H140');
text(116,101,'да',2.5); text(116,151,'да',2.5); text(116,176,'да',2.5)
text(112,230,'Idle / NonTarget / Lunch → лимит политики',3)
text(112,236,'Остальные категории: правило отсутствует',3)

# Basic title block, intentionally unassigned document designation.
svg.append('<rect x="20" y="247" width="185" height="45" fill="white" stroke="black" stroke-width="0.5"/>')
for d in ['M20,257 H205','M20,267 H205','M20,277 H80','M80,247 V292','M175,247 V292']:
    svg.append(f'<path d="{d}" fill="none" stroke="black" stroke-width="0.25"/>')
text(127.5,254,'Обозначение: согласовать',3)
text(127.5,264,'WorkPulse – детектор нарушений',3.2)
text(127.5,278,'Схема алгоритма',3.2)
text(127.5,284,'БГУИР, гр. 314301',3)
text(23,254,'Разраб. Лихтар И.В.',2.8,'start')
text(23,264,'Пров. Котько Е.Н.',2.8,'start')
text(23,274,'Подпись / дата:',2.8,'start')
text(190,254,'Лист 1',2.8);text(190,264,'Листов 1',2.8);text(190,279,'A4',3)
svg.append('</g></svg>')
out.write_text('\n'.join(svg), encoding='utf-8')
print(out)
