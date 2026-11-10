# Сторонний код

## AR-Game-Beginners-Tutorial (Alive Studios)

- Источник: https://github.com/Alive-hub/AR-Game-Beginners-Tutorial
- Лицензия: Apache License 2.0 — https://www.apache.org/licenses/LICENSE-2.0

Следующие файлы основаны на скриптах из этого репозитория и **изменены**:

| Файл | Изменения |
|---|---|
| `Assets/Scripts/UIButtonHandler.cs` | Добавлены режим строительства, подсказки, панель фигур; «Старт» доступен только после установки фигуры |
| `Assets/Scripts/EnablePhysicsOnEvent.cs` | Подписка в `Awake`, автоматический поиск `Rigidbody` |
| `Assets/Scripts/ResetRbPositionOfObject.cs` | Позиция запоминается при нажатии «Старт»; API Unity 6 (`linearVelocity`) |
| `Assets/Scripts/ShootBallLogic.cs` | `Camera.main`, скорость через `ForceMode.VelocityChange`, отписка от событий |

Скрипт `HideBarOnStart.cs` и ассет AR Magic Bar Lite не используются — вместо них написан `BuildController.cs`.

Остальной код проекта написан заново.
