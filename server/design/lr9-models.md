# UML ЛР9

Редактируемые исходники и локально построенные SVG/PNG:

| Модель | Файл |
|---|---|
| Варианты использования | [lr9-use-cases.puml](lr9-use-cases.puml) |
| Основные классы и зависимости | [lr9-classes.puml](lr9-classes.puml) |
| Фактическое развёртывание | [lr9-deployment.puml](lr9-deployment.puml) |

Схемы описывают реализованную версию ЛР9. Исходные диаграммы ЛР8 сохранены как исторические материалы предыдущего этапа. CSV создаётся в браузере из сохранённого снимка; HTTP-маршрута PDF/XLSX здесь нет. Уведомления, HRM/ERP и KPI не показаны как реализованные компоненты.

Рендер из корня через ранее скачанный PlantUML MIT 1.2026.8:

```powershell
java '-Djava.awt.headless=true' -jar tmp/lr8/plantuml.jar -charset UTF-8 -tsvg 'server/design/lr9-*.puml'
java '-Djava.awt.headless=true' -jar tmp/lr8/plantuml.jar -charset UTF-8 -tpng 'server/design/lr9-*.puml'
```

Выбор библиотек, границы принципов SOLID/DRY/KISS/DI и примеры контрактов: [api-contract.md](../api-contract.md).
