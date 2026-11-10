# AR Ball Game

AR-игра для Android на Unity: строишь на полу комнаты постройку из фигур, а потом сбиваешь её шарами, которые вылетают из камеры телефона.

Проект сделан по туториалу [How to make an AR Game in Unity (Beginner Friendly Tutorial)](https://www.youtube.com/watch?v=XZPNRxMvelc) от Alive Studios и расширен: вместо платного ассета AR Magic Bar — свой режим строительства, плюс фигуры, готовые шаблоны, счётчик, звуки и иконка.

## Возможности

- **AR на ARCore**: поиск горизонтальных поверхностей (пол, стол), фигуры стоят в реальной комнате.
- **Строительство**: лента из 10 фигур (куб, брусок, колонна, домино, плита, цилиндр, шар, горка, пирамида, арка). Тап по полу — фигура на пол, тап по фигуре — ставится сверху или сбоку. Кнопки «Отмена», «Повернуть» (на 45°), «Очистить».
- **Готовые шаблоны**: Башня, Дженга, Домик, Стена, Замок, Беседка, Домино-змейка, Мишени, Круг арок.
- **Игра**: «СТАРТ» включает физику, «ОГОНЬ!» стреляет шаром из камеры, «Сброс» возвращает всё на место. Счётчик «Сбито: X / Y».
- **Звуки**: синтезированы кодом (без скачанных файлов) — удары дерева и шара, выстрел, установка фигуры, кнопки, фанфары за победу.
- **Иконка**: нарисована скриптом `Tools/make_icon.py`, адаптивная для Android.

## Скачать

Готовый APK — в разделе [Releases](../../releases). Нужен Android 8+ с поддержкой ARCore (при первом запуске телефон может предложить установить «Сервисы Google Play для AR»).

## Как устроен проект

| Папка / файл | Что там |
|---|---|
| `Assets/Scripts/UIButtonHandler.cs` | Кнопки Старт / Огонь / Сброс и переключение режимов (события, как в туториале) |
| `Assets/Scripts/BuildController.cs` | Режим строительства: установка фигур тапом, отмена, поворот, очистка |
| `Assets/Scripts/ShootBallLogic.cs` | Выстрел шаром из камеры |
| `Assets/Scripts/EnablePhysicsOnEvent.cs` | Включение физики по кнопке «Старт» |
| `Assets/Scripts/ResetRbPositionOfObject.cs` | Возврат фигур на место по «Сбросу» |
| `Assets/Scripts/ScoreCounter.cs` | Счётчик сбитых фигур |
| `Assets/Scripts/SoundManager.cs`, `ImpactSound.cs` | Звуки |
| `Assets/Editor/ARGameSetup.cs` | Собирает сцену, UI, настройки Android/ARCore и APK одной командой |
| `Assets/Editor/ARGameShapes.cs`, `ARGameTemplates.cs` | Генерация фигур, шаблонов и их иконок |
| `Assets/Editor/ARGameAudio.cs` | Синтез звуков в WAV |
| `Assets/Editor/ARGameTemplateCheck.cs` | Проверка шаблонов: нет пересечений, не падают сами |
| `Tools/make_icon.py` | Рисует иконку приложения (Python + Pillow) |

## Как собрать

1. Unity **6000.6.0f1** с модулем **Android Build Support** (SDK, NDK, OpenJDK).
2. Открыть папку проекта в Unity Hub (пакеты AR Foundation 6.6.2 и ARCore подтянутся сами).
3. Меню **AR Game → Setup Project** — создаёт сцену `Assets/Scenes/ARGame.unity`, фигуры, звуки и настройки.
4. Меню **AR Game → Build APK** — собирает `Builds/ARBallGame.apk`.

Иконку можно перерисовать: `python Tools/make_icon.py` (нужен Pillow), затем снова **Setup Project**.

## Технологии

Unity 6 · AR Foundation 6.6 · Google ARCore · Input System · uGUI · C#

## Благодарности

Идея и исходные скрипты — туториал Alive Studios, код: [Alive-hub/AR-Game-Beginners-Tutorial](https://github.com/Alive-hub/AR-Game-Beginners-Tutorial) (Apache License 2.0). Подробности — в [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
