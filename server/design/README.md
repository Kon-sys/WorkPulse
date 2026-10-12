# Материалы моделирования ЛР8

Исходники `.puml` редактируемы; `.svg` предназначены для масштабирования и будущего отчёта, `.png` – для быстрого просмотра. Диаграммы построены локально PlantUML MIT 1.2026.8. Данные не отправлялись на онлайн-сервер рендеринга.

| Файл | Содержание |
|---|---|
| classes.puml | Классы фактического кода и применённые паттерны |
| components.puml | Реализованные компоненты и явно обозначенные будущие API/Infrastructure/Agent |
| deployment.puml | Фактическое размещение демо и целевая архитектура |
| packages.puml | Зависимости проектов |
| session-state.puml | Состояния сессии и сторожевые условия переходов |
| analysis-sequence.puml | Один основной поток взаимодействия объектов анализа |
| analysis-activity.puml | Деятельность при анализе событий |
| violation-drawing.svg | Схема алгоритма детектора на координатной сетке с рамкой и основной надписью |
| algorithms.md | Текстовое описание, правила и границы реализации |

## Локальный рендер UML

Java 21 уже установлен на компьютере. Скачать официальный бесплатный JAR и построить диаграммы:

```powershell
New-Item -ItemType Directory -Path tmp/lr8 -Force | Out-Null
Invoke-WebRequest -Uri 'https://github.com/plantuml/plantuml/releases/download/v1.2026.8/plantuml-mit-1.2026.8.jar' -OutFile tmp/lr8/plantuml.jar
java -jar tmp/lr8/plantuml.jar -charset UTF-8 -tsvg 'server/design/*.puml'
java -jar tmp/lr8/plantuml.jar -charset UTF-8 -tpng 'server/design/*.puml'
```

Чертёж – исходный SVG, редактируемый в векторном редакторе; regenerate: `python server/design/draw-algorithm.py` (только стандартная библиотека Python). Сохранены узлы алгоритма, координаты, сетка, ортогональные соединения и размер листа A4. Это подготовленный чертёж для согласования оформления, а не заявление о полной проверке всех ГОСТ. Методичка ЛР8 ссылается на отсутствующее задание ЛР3 для основной надписи; обозначение документа и точный шаблон необходимо сверить перед отчётом. Поля обозначения и даты подписи не выдуманы.

## Трассировка

- Сессии и перерывы: `WorkPulse.Domain/WorkSession.cs` → session-state.
- Реконструкция: `WorkPulse.Application/ActivityReconstructor.cs` → activity и sequence.
- Детектор: `WorkPulse.Application/Classification.cs`, метод Detect → violation-drawing.
- Сценарий и порт: `WorkPulse.Application/AnalysisService.cs` → classes и sequence.
- Фактические сборки: WorkPulse.slnx и ProjectReference → packages/components/deployment.

Предметные классы показаны выборочно, а не как перечень всех служебных типов C#. Узел `Program` на диаграмме последовательности соответствует генерируемому компилятором классу top-level программы.
