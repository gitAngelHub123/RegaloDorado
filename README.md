# Regalo Dorado (Golden Present)

A [MelonLoader](https://melonwiki.xyz) mod for **Plants vs. Zombies: Replanted** that adds a **Golden Present** to Crazy Dave's store, inspired by the Gold Sprout from the mobile version of PvZ.

*Versión en español más abajo.*

## Features

- New item on **page 4** of the store (next to the Mushroom Garden and Aquarium).
- Costs **$50,000** and can be bought **once per day** per profile.
- Always contains a **Zen Garden plant you haven't collected yet**, so you can finish the plant collection achievement without farming presents for hours.
- If you already collected them all, it gives one that isn't currently in your garden.
- Can't be bought while the Zen Garden is full.
- Golden present icon and texts in the game language: English, Spanish, Portuguese, French and German. Other languages show English.

## Requirements

- MelonLoader 0.7.3 or newer
- .NET 6.0 Desktop Runtime (required by MelonLoader for IL2CPP games)
- Tested on PvZ Replanted version 1.5.1469 (Steam)

## Installation

1. Install MelonLoader into the game and launch it once.
2. Download `RegaloDorado-vX.Y.Z.zip` from [Releases](../../releases).
3. Copy `Mods/RegaloDorado.dll` into the game's `Mods` folder (`...\steamapps\common\PVZ Replanted\Mods\`).
4. Launch the game. The MelonLoader console should show `Regalo Dorado cargado`.

To uninstall, delete `RegaloDorado.dll` from the `Mods` folder.

## Building from source

The project references the game's MelonLoader assemblies, so the game must be installed with MelonLoader and launched once to generate `MelonLoader/Il2CppAssemblies`.

```sh
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\PVZ Replanted"
```

`GameDir` defaults to the Linux Steam path (`~/.local/share/Steam/steamapps/common/PVZ Replanted`). The output is `bin/Release/net6.0/RegaloDorado.dll`.

## How it works

The store items are data assets (`StoreEntryData`). The mod clones the Marigold entry, places it in a free slot, and uses Harmony patches on `StoreEntryModel` and `StoreModel` to handle its price, daily limit and purchase. The purchase follows the same steps the game uses to buy a Marigold, but with the chosen plant. The icon is the game's own present sprite, recolored at runtime. No game files are modified or redistributed.

---

## Español

Mod de MelonLoader para **Plants vs. Zombies: Replanted** que añade un **Regalo Dorado** a la tienda de Crazy Dave, inspirado en el Brote Dorado de la versión móvil.

- Está en la **página 4** de la tienda.
- Cuesta **$50.000** y se puede comprar **una vez al día** por perfil.
- Siempre trae una **planta del Jardín Zen que todavía no has conseguido**. Sirve para completar el logro de la colección sin farmear regalos durante horas.
- No se puede comprar si el Jardín Zen está lleno.

**Instalación:** instala MelonLoader, descarga el zip de [Releases](../../releases) y copia `RegaloDorado.dll` en la carpeta `Mods` del juego.

## License

[MIT](LICENSE) © 2026 Angel
