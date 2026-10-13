# Grandma's House

A first-person comfort game about living Grandma's homestead life in Star Valley, Wyoming. You grow food, preserve it, store it, and share it through the seasons. Every task is done by hand at a "station". Grandma's story is discovered through the notes, diary, recipe cards, and jars she left behind, never told directly.

## The design doc is the source of truth

**Design doc:** https://claude.ai/code/artifact/e30813e2-ed8f-439c-8954-9add1553c5e3 (a Claude Doc; read and edit it with the Claude Docs tools, never by web fetch).

Every session working on this project must:

1. **Read before working.** Check the design doc sections that relate to the task, plus "Roadmap → Current phase".
2. **Update after working.**
   - Tick off finished items under Current phase.
   - Bring any section that changed up to date.
   - Add a row to the **Decision log** (Date · Decision · Why) for every meaningful decision, newest first. When a decision changes, add a new row; don't rewrite old rows.
3. **Test every idea** against: *Does this make you feel like you're living Grandma's life, at her pace?* If an idea adds pressure, danger, or optimizing, it goes on the later list or gets dropped.

## Current phase: Phase 1, Vertical slice

Goal: one full loop from seed to sealed jar. Plant cucumbers and dill, tend them, harvest, make dill pickles, can them, shelve them, and find Grandma's first note. Menus, rebinding, and a PC button-prompt style choice (Auto, Xbox, PlayStation, Switch) land late in the phase. The checklist is in the design doc under Roadmap.

## Key decisions (summary; reasons are in the doc's Decision log)

- **Engine:** Unity 6 (6000.4.0f1), URP (Universal 3D template). Stylized, soft art.
- **Platforms:** PC first, with full controller support from day one. Consoles come after launch, and the Switch sets the performance budget.
- **Launch:** Cozy mode only. Homesteader (hard mode) is parked. Keep numbers in data (ScriptableObjects) so it stays possible, but build nothing that only Homesteader needs.
- **Stations:** every activity is a hands-on station (Schedule I style). The camera moves into a working view, and you use tools directly.
- **Carrying:** hands hold one item, tool, or container at a time. Containers include the basket, egg basket, 12-jar crate, firewood carrier, watering can, and creel. A 4 to 6 slot tool belt is picked from a quick wheel. Stations reach into storage in the same room. No weight limits, no inventory screen.
- **Time:** a long real-time day (about 40 to 60 minutes, to be tuned) in four parts. The clock exists only in the world, never on screen. The sun stops once night falls, and the next day starts when you sleep. Meals don't move time forward.
- **Meals:** bonuses only, never penalties (like Valheim's food), plus a variety bonus.
- **Economy:** no cash. Neighbors barter directly; the general store keeps a credit ledger on Grandma's account.
- **Tone:** no religion (only cultural staples), no politics, war, or hard times. Grandma is fictional and appears only through text and a blurred painting.

## Code conventions

- C# scripts live in `Assets/_Project/Scripts/`, grouped by system (`Player/`, `Interaction/`, `Stations/`, `Carrying/`, `Garden/`, `Kitchen/`, `UI/`, `Saving/`, `Time/`, `Data/`).
- Use the **new Input System** only, with one action map that works for both keyboard/mouse and gamepad.
- Never hard-code a key or button name in on-screen text; prompts go through the prompt-icon system (coming in Phase 1).
- Gameplay numbers (speeds, durations, capacities, mode values) live in ScriptableObjects under `Assets/_Project/Data/`, never hard-coded.
- Keep the scripts small and readable; match the style of the code around them.
- Prefer composition: stations, containers, and pickups are components on GameObjects.

## Folder layout

```
Assets/_Project/
  Scripts/      gameplay code, grouped by system
  Data/         ScriptableObject assets (items, recipes, mode profiles)
  Prefabs/
  Scenes/       Title.unity starts the game; Homestead.unity is the main scene (kitchen, pantry, bedroom, garden); Prototype.unity is the old test room
  Art/          models, materials, textures
  Audio/
```
