# Project Structure

Unity 6000.6.8f1, URP, Android

## Folders

- `Assets/Scripts/` — C# code organized by feature (`App`, `Waves`, `Guests`, `Buildings`, `Staff`, `UI`, etc.)
- `Assets/Data/` — game configuration (waves, buildings, economy, progression)
- `Assets/Prefabs/` — reusable game objects and UI
- `Assets/Scenes/` — game scenes and `Testing/` scenes
- `Assets/Art/` — models, textures, materials, animations, shaders and VFX
- `Assets/Audio/` — music and sound effects
- `Assets/Settings/` — Unity rendering and input assets
- `Assets/Editor/`, `Assets/Tests/`, `Assets/ThirdParty/` — editor tools, tests and external assets when neede
- `docs/design/` — design documents (GDD, prototype/MVP specification, UI screens)

## Basic rules

- Each system owns its logic and state; UI displays data and sends player actions
- Keep configuration in `Data/`; do not put player save files in `Assets/`
- Commit Unity assets together with their `.meta` files. Do not commit generated folders such as `Library/` or `Temp/`
- Add folders and dependencies only when they are needed. Keep the structure consistent