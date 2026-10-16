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
3. **No hand-holding.** Don't block steps until earlier ones are done, and don't show reminders like "needs salt" or "add lemon juice first". Let the player do things in any order or skip them; the result shows in what they made (a jar that isn't one of Grandma's recipes is "Don't eat!"). Button prompts for what you can do right now are fine.
4. **Test every idea** against: *Does this make you feel like you're living Grandma's life, at her pace?* If an idea adds pressure, danger, or optimizing, it goes on the later list or gets dropped.

## Current phase: Phase 2, Garden depth

Goal: tomatoes from seed to jar. Grow them on a stake, pinch suckers, pick off hornworms, plant marigolds as companions, then can whole tomatoes (lemon juice, no blanching) and salsa (onion and jalapeño crops; chopped pieces are scraped from the board into the cooking pot, cooked with vinegar and salt, and ladled into jars). All built; next is tweaking from a friend's playtest of the 0.1.0 Windows build (builds go in `C:\Dev\GrandmasHouse-Builds`, outside the repo). The checklist is in the design doc under Roadmap. Phase 1 (seed to sealed jar for dill pickles and pickled garlic) is built; its outside-playtester gate is still open.

## Key decisions (summary; reasons are in the doc's Decision log)

- **Engine:** Unity 6 (6000.4.0f1), URP (Universal 3D template). Stylized, soft art.
- **Platforms:** PC first, with full controller support from day one. Consoles come after launch, and the Switch sets the performance budget.
- **Launch:** Cozy mode only. Homesteader (hard mode) is parked. Keep numbers in data (ScriptableObjects) so it stays possible, but build nothing that only Homesteader needs.
- **Stations:** the hands-on craft steps are stations (Schedule I style): cutting and packing jars. Brine is made by hand in the world: fill the pot at the sink, pour vinegar and shake salt from the pantry staples, set it on a burner. The camera moves into a working view, and you use tools directly. Moving things around happens in the world without entering a station: carrying jars, pots and the canner on and off the stove, the canner lid, and the canner itself (no station). The garden has no station either: plant, water (hold Interact with the can; it tips over where you look), pull weeds (hold Interact to grab, then drag the mouse or right stick back; the view holds still while the weed stretches until it pops out), and pick ripe fruit from where you stand. The one garden station is the close look at a single plant (`PlantStation`): press "Look closer" on a plant, turn it with Previous/Next or left/right, and pinch suckers, pick off hornworms, or pick what's ripe.
- **Carrying:** two hands, each holding a few things of one kind (`Pickup.perHand`: jalapeños 4, garlic/dill 3, tomatoes/onions 2, everything else 1); pots, the canner, baskets and the watering can take both hands (`Pickup.NeedsBothHands`). `PlayerHands.Held` is the last thing picked up, used first; `Carried` lists everything. Containers include the basket, egg basket, 12-jar crate, firewood carrier, watering can, and creel. A 4 to 6 slot tool belt is picked from a quick wheel. Stations reach into storage in the same room. No weight limits, no inventory screen.
- **Time:** a long real-time day (about 40 to 60 minutes, to be tuned) in four parts. The clock exists only in the world, never on screen. The sun stops once night falls, and the next day starts when you sleep. Meals don't move time forward.
- **Meals:** bonuses only, never penalties (like Valheim's food), plus a variety bonus.
- **Economy:** no cash. Neighbors barter directly; the general store keeps a credit ledger on Grandma's account.
- **Tone:** no religion (only cultural staples), no politics, war, or hard times. Grandma is fictional and appears only through text and a blurred painting.

## Code conventions

- C# scripts live in `Assets/_Project/Scripts/`, grouped by system (`Player/`, `Interaction/`, `Stations/`, `Carrying/`, `Garden/`, `Kitchen/`, `Audio/`, `UI/`, `Saving/`, `Time/`, `Data/`).
- Use the **new Input System** only, with one action map that works for both keyboard/mouse and gamepad.
- Never hard-code a key or button name in on-screen text; prompts go through the prompt-icon system (coming in Phase 1).
- Gameplay numbers (speeds, durations, capacities, mode values) live in ScriptableObjects under `Assets/_Project/Data/`, never hard-coded.
- Keep the scripts small and readable; match the style of the code around them.
- Prefer composition: stations, containers, and pickups are components on GameObjects.
- Dev menu (Editor and development builds only, `Scripts/DevTools/DevMenu` on the Homestead "Dev Tools" object): F1 while walking opens it; click a category (from each Pickup's `ItemCategory`) to see its items, pick how many, then click any item from `Data/SaveCatalog` to spawn it in front of the player. Items added to the catalog appear automatically; add future testing tools (time skip, etc.) to this menu rather than new hotkeys.
- Garden tending: a crop opts into suckers and pests through `CropData`'s Tending fields; both use the `Pinchable` component (taken off only in `PlantStation`'s close look) and appear at stage-prefab children named `SuckerPoint`/`PestPoint` (`HarvestPoint` for the harvest). A plant with suckers, pests or crowding weeds loses its tended bonus that night.
- Cutting board: produce set down while the board is busy waits in `WaitingRow` (up to six); a held basket empties onto it; the next piece rolls forward when one is finished. Pieces are named after their produce ("Onion Slice", "Tomato Wedge", "Clove"), which is how the pot and saves tell them apart.
- What a jar is: `JarContents.Recipe` picks the first recipe in the PintJar prefab's `recipes` list (Grandma's real recipes only) whose `needs`/`mostly` its contents meet; anything else is the `inedible` recipe, "Don't eat!" with a trash-can label. A new recipe needs its RecipeData, a place in that list, and a label (`Art/Textures/LabelText_*.png`: the picture with the name printed under it, drawn with System.Drawing).
- Handfuls: with empty hands, look at a pile on the cutting board and press Pick up for a `Handful` (a both-hands Pickup made at runtime; can't be set down, isn't saved). Drop it into an open jar or the cooking pot, or put it back on the board.
- Co-op groundwork (online co-op is being considered; see the doc's Co-op section): add no new statics or lone `FindAnyObjectByType` lookups for "the player" (pass the player in); route every world change through one method on its owner (like `GardenBed.PullOut(weed)`) so it can become a network call; keep visuals (stretch, wobble, steam) apart from state (pulled out, sealed); keep random rolls in one place the host can own.
- Saving: a new item prefab needs a `SavedItem` key and an entry in `Data/SaveCatalog`; extra item state implements `IItemState`, scene systems implement `ISaveable`, and stations that hold things re-adopt them in `IAfterLoad`.

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
