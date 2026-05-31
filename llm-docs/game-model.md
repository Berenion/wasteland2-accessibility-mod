# Wasteland 2 Director's Cut — Game Model Reference

Structural reference for screen-reader accessibility work. Audience: a blind developer (and assisting LLMs) who needs conceptual context but cannot see the screen. Not a walkthrough. Director's Cut released 2015 (PC re-release of 2014 original) on Unity 4.x with NGUI for UI.

## 1. Core Paradigm

- Squad-based post-apocalyptic CRPG. Top-down isometric perspective with a freely-rotating/zoomable camera. Spiritual sequel to Fallout 1/2's design lineage (which itself descended from the original Wasteland, 1988).
- Setting: irradiated American Southwest (Arizona in Act 1, Los Angeles in Act 2). The player commands a squad of Desert Rangers.
- Two-mode gameplay loop:
  - **Exploration mode**: real-time movement on local maps and on a stylized world map. Party moves as a unit. Skill checks, dialogue, looting, environment interaction happen here.
  - **Combat mode**: turn-based, action-point driven, triggered by enemy detection or scripted ambush. Each ranger and enemy gets a turn ordered by initiative; combat ends when one side is wiped or flees.
- Persistent branching narrative. Choices (who to save, who to side with, dialogue outcomes) propagate through the rest of the game; many quests have mutually exclusive resolutions. World state is saved per-slot.

## 2. Spatial Model

### Movement granularity
- **Free movement**, NOT tile-locked, on local maps. Click-to-move (or stick-to-move with controller). Pathfinding routes the squad around obstacles.
- Internally the engine still grids combat for AP costs and cover calculations, but the player perceives free positioning rather than explicit tiles.

### World map vs local map
- **Local maps**: the rendered 3D scenes where actual play happens (Ranger Citadel, Highpool, Ag Center, towns, dungeons, etc.).
- **World map**: a stylized overview of the region used purely for travel between local maps. Time passes on world map (relevant for timed quests, water consumption, radiation exposure). Random encounters can interrupt travel.
- Locations on the world map are revealed by exploration and by intel from NPCs/radio. The **Outdoorsman** skill increases the radius at which hidden world-map locations (oases, caches, encounters) become visible.

### Fog of war / line of sight
- Local maps use fog of war: unexplored areas hidden, explored areas dimmed when out of sight. Visible (lit) areas reflect current LOS.
- Vision radius is driven by the Awareness/Perception attribute. Formula referenced by community: detection radius ~ flat base + ~0.6 per Perception point.
- LOS matters tactically: enemies can be flanked or stealth-killed before they raise an alert. Cover blocks LOS and grants defensive bonuses in combat.
- The **Perception** stat also surfaces hidden objects: traps, mines, dig spots, hidden containers. These are highlighted within range when a ranger is close enough.

### Combat positioning
- On combat entry, the camera locks and a turn order forms.
- Movement in combat costs AP. **Combat Speed** is "AP per meter" — higher SPD lets a character cover more distance per AP.
- Cover comes in implicit/half/full tiers based on intervening geometry; affects hit chance and damage taken.
- Crouching reduces visibility/profile and improves accuracy; ambush (overwatch) reserves AP to fire on the first enemy entering LOS during the enemy turn.

## 3. Party / Character System

### Party composition
- **Up to 7 active members.** Standard build: 4 player-created Rangers + up to 3 recruited NPCs (CNPCs).
- If you start with fewer than 4 created Rangers, you can recruit more CNPCs to reach the cap of 7.
- Extra recruits beyond the cap can be parked at HQ but do not gain XP.
- CNPCs may "go rogue" mid-combat (act on their own) — chance reduced by the **Leadership** skill of nearby ranger.

### Attributes — the "CLASSIC" system (28 points to allocate at creation, +1 every 10 levels)
- **C**oordination — accuracy with firearms, fine motor skills.
- **L**uck — chance-based bonuses: extra HP on level, dodging lethal hits, crit-like rolls.
- **A**wareness — perception/vision range, initiative input.
- **S**trength — carry capacity, melee/heavy weapon damage, CON contribution.
- **S**peed — combat speed (movement-per-AP), initiative input.
- **I**ntelligence — skill points per level.
- **C**harisma — leadership influence, CNPC follow chance, group XP modifier.

### Derived stats
- **Action Points (AP)** — from Coordination/Speed.
- **Combat Initiative** — `5 + AWA + SPD/2`.
- **Combat Speed** — distance per AP, from Speed.
- **CON** — hit-point pool, from Strength.
- **Evasion** — passive miss chance.
- **Skill Points per level** — primarily from Intelligence.

### Skill categories
Director's Cut has four skill branches; each skill ranges 1–10.

- **Combat skills** (one weapon proficiency per category):
  - Handguns, Submachine Guns, Shotguns, Assault Rifles, Sniper Rifles, Heavy Weapons, Energy Weapons, Bladed Weapons, Blunt Weapons, Brawling.
- **General skills** (party-wide utility / social):
  - Leadership, Outdoorsman, Hard Ass, Smart Ass, Kiss Ass, Animal Whisperer, Brute Force, Barter, Weaponsmithing, Perception, Field Medic.
  - (Field Medic is sometimes grouped under Knowledge in some references; in the in-game UI it appears with general/medical skills.)
- **Knowledge skills** (technical / "tag" skills):
  - Alarm Disarming, Computer Science, Demolitions, Lockpicking, Mechanical Repair, Safecracking, Surgeon, Toaster Repair.
- **Special / hidden skills** appear in some references, typically tied to specific perks or quirks, not user-allocated.

### Quirks (Director's Cut addition; optional, max one per ranger, chosen at creation)
Each quirk is a tradeoff — bonus paired with a downside. Examples:
- **Ascetic** — +5 skill points, +1 attribute, but cannot use gadgets.
- **Brittle Bones** — +1 AP, but takes extra damage.
- **Delayed Gratification** — +1 skill point per level for 20 levels, delayed reward.
- **Disparnumerophobia** — alternates +2/-2 attribute swing per level.
- **Psychopath** — bonus crit damage, penalties to social skills.
- ~14 quirks total in DC; full list on the wiki.

### Backgrounds
- Each ranger picks a Background at creation (e.g. Cop, Doctor, Mechanic, Farmer, etc.). Background grants flavor dialogue tags and small starting bonuses; less mechanically heavy than Quirks.

### Perks
- Perks are unlocked by reaching skill thresholds (each skill has its own perk tree). Earned automatically on hitting the rank, then chosen from available unlocked options. Perks are mostly passive (e.g. damage bonuses, AP refunds, status effect chances).

### Health / status states
- **Conscious** — normal, CON above 0.
- **Unconscious** — CON hit 0; ranger goes down with a countdown timer above their head. Cannot act. Will auto-revive after timer if not finished off, but vulnerable in the meantime.
- **Critically wounded / Comatose** — deeper negatives below 0; requires Surgeon skill + Trauma Kit to revive. Failure can cause permadeath.
- **Dead** — permanent. Created rangers are gone for good; CNPCs typically remain dead but story may vary.
- **Wounded / Bleeding / Diseased / Poisoned / Stunned / Crippled (limb)** — status effects that tick or impair. Cleared by appropriate consumable or Field Medic check.

### Radiation
- Carried as an exposure level on each ranger. World-map radiation clouds and certain location hazards add exposure.
- Mitigated by **Rad Suits** (multiple tiers; higher tiers survive denser clouds). Some clouds are lethal regardless of suit.
- High exposure causes CON damage and can trigger sickness; rest/medical items reduce exposure.

## 4. Combat Mechanics

### Turn structure
- Initiative-ordered, side-by-side: each combatant's turn comes around individually based on initiative (not strict squad-vs-squad rounds).
- **End Turn** ends current ranger; can also pass turn.
- Reserved AP can fuel **Ambush (overwatch)** — auto-fire on the first valid enemy entering LOS before next turn.

### Action point economy
- Movement: AP per meter, scaled by Speed (each ranger has different effective range per AP).
- Attacks: each weapon has a single-shot AP cost and (if applicable) burst-fire cost. Examples (non-exhaustive, varies per gun):
  - Sniper rifle: typically 7 AP per shot.
  - Assault rifle burst: up to ~8 AP.
  - Reload: usually shoot-cost or shoot-cost + 1.
- Skill use in combat (Field Medic, Surgeon, Computer Science to hack turrets, Demolitions to plant/disarm) all cost AP.
- Swap weapon, crouch, change ammo type — all AP-costed.

### Hit / damage rolls
- Per-shot **Chance to Hit** displayed at target (percent). Affected by: weapon skill, distance vs weapon's optimal range bands, target evasion, cover, called-shot penalty, status effects, lighting.
- **Critical hits**: weapon-typical crit chance + bonuses from Luck, perks, called-shot location.
- **Armor**: damage reduction subtracted from incoming damage. **Armor penetration** stat on weapons/ammo reduces target armor.
- Damage roll typically a min-max range per weapon, modified by skill/perks.

### Precision strikes (Director's Cut feature — called shots)
- Aim at specific body part. Penalty to hit but with effect on success:
  - **Head** — 1.35x damage, +25% crit, can confuse/psyche enemy. High to-hit penalty.
  - **Torso** — 0.8x damage, reduces enemy armor. Small to-hit penalty.
  - **Arms** — 0.7x damage, reduces enemy chance-to-hit, can destroy/explode their weapon. Moderate penalty.
  - **Legs** — 0.7x damage, reduces enemy combat speed/AP, knockdown chance. Moderate penalty.
- Body-part labels rename per enemy archetype (robot CPU/chassis, etc.).
- Heavy weapons, machine pistols and shotguns generally cannot precision-strike (AOE/spread weapons).

### Other combat actions
- Free aim / suppressing fire (heavy weapons / autos).
- Throw grenades / molotovs (Demolitions augments).
- Use medkit / trauma kit (Field Medic / Surgeon).
- Hack turret / robot (Computer Science) — flips it temporarily to your side.
- Plant / disarm explosives (Demolitions).
- Flee combat — disengage; enemies may pursue.

## 5. Inventory and Equipment

### Personal inventory
- Each ranger has their own inventory and weight capacity (driven by Strength). Overweight slows movement.
- Backpacks/quick-access exist for ammo and consumables; some items show on the action bar.

### Equipment slots
- **Weapon 1** + **Weapon 2** (loadout slots; swap is an in-combat action with AP cost).
- **Armor** (torso/body armor — primary defense).
- **Headgear** (helmets/hats — defense and skill bonuses).
- **Trinket** / **Accessory** slot(s) — character-specific items (often quirky boosts).
- Cosmetic apparel slots (head, torso, legs, backpack) — visual only, original 4 created rangers only.

### Items / categories
- **Weapons** — broad list across the 10 combat skills. Each weapon: AP cost, damage range, range bands, magazine, ammo type, AP-burst, mod slots.
- **Ammunition** — typed per weapon family (e.g. 9mm, 5.56, 7.62, shotgun shells, energy cells, rockets, throwing weapons). Ammo types include AP/HP/explosive variants on some.
- **Armor / clothing** — coats, vests, suits, helmets, eyewear.
- **Consumables** — medkits, trauma kits, anti-rad meds, drugs/stims (may have addiction), food, doctor bag uses.
- **Mods / attachments** — scope, suppressor, magazine, stock, etc. Applied via **Weaponsmithing**.
- **Key items / quest items** — story items (quest tokens, keycards, audio logs, books).
- **Junk / trade goods** — sellable; "flag as junk" right-click bulk-sells next vendor visit.
- **Skill books** — single-use; grant +1 rank in a non-combat skill.

### Stash / shared storage
- The **Ranger Citadel** unlocks shared stash chests after the early-game Highpool/Ag Center choice and powering up the radio repeater. Stash is shared across the squad and persistent.
- Inventory transfer between adjacent party members is done by drag-drop in the inventory screen.

### Looting
- Containers (crates, lockers, safes, refrigerators), corpses, hidden caches.
- Some require skill checks: **Lockpicking** (locked containers), **Safecracking** (safes), **Alarm Disarming** (trapped containers), **Perception** to see hidden ones, **Brute Force** to smash open.
- Looting opens a **Loot panel** with take-all and per-item options.

## 6. Dialog and Dialogue System

### Keyword-based
- Classic Wasteland-lineage dialogue: NPC speaks; **keywords** are extracted from their text and listed at the bottom as clickable topics. Selecting a keyword asks them about it; their response may surface new keywords. Continues until you exhaust topics or an exit.
- Keywords appearing inline within NPC text are highlighted (typically in color) to show they are askable.
- **Hidden keywords**: some words don't auto-list but can be **typed in** via the freeform input box. This was a callback to original Wasteland design and is preserved in DC. Can unlock unique branches.

### Skill-gated responses
- Three "speech" skills, all in General:
  - **Hard Ass** — intimidate.
  - **Smart Ass** — outwit / verbal trick.
  - **Kiss Ass** — flatter / charm.
- Skill-gated dialogue options appear with the required skill name and rank. If a *party member* meets the rank, you select that member then choose the option.
- Other skills can also gate dialogue: e.g. **Animal Whisperer** with animal NPCs, **Toaster Repair** for the toaster-themed easter egg branches, **Computer Science** with terminals/AIs, etc.

### Faction / reputation impact
- Wasteland 2 does NOT use a numeric faction reputation scale (that was added in Wasteland 3). Faction "standing" is tracked through scripted flags (quests completed, NPCs killed/spared, factions sided with).
- These flags drive gating: hostile factions attack on sight, allied factions open vendors or quests, and major story branches lock out content from rival factions.

### Branching choices
- Dialogue can lead to permanent quest outcomes (kill X, side with Y, betray Z). Exit-from-dialog and combat-from-dialog transitions are common.

## 7. UI Screens (enumerated for state-machine grep)

These are the primary screens/panels the mod's state router cares about. Names match how a player would refer to them and are intended to be cross-referenceable to decompiled class names.

### Front-end
- **Main Menu** — Continue / New Game / Load / Options / Credits / Quit. Initial focus on Continue when a save exists.
- **Load Game** — slot list with metadata.
- **Save Game** — slot list with overwrite/new-slot flow.
- **Options** — sub-tabs: Gameplay, Audio, Video, Controls/Input, Language. Confirmation modal on apply/discard.
- **New Game / Difficulty Select** — pick difficulty and other start flags.
- **Character Creation** — multi-step:
  - Squad overview / pick slot
  - Identity (portrait, name, gender, voice)
  - Attributes (CLASSIC point spend)
  - Skills (initial skill points)
  - Quirk selection (single optional)
  - Background selection
  - Bio/summary review
  - Each step has its own panel and a tutorial popup (TutorialPopupMenu) on first entry.

### In-game screens
- **In-Game HUD (exploration)** — party portraits, action bar, radio button, world-map button, mini-status, time indicator.
- **In-Game HUD (combat)** — initiative tracker, AP gauges, end-turn button, ambush/crouch/swap quick actions, target reticle with Chance-to-Hit panel, precision-strike body-part selector.
- **World Map** — overview of region with travel cursor, location pins, encounter overlays, time-of-day, party speed indicator. Sub-modes: travel and overview.
- **Local Map / Mini-Map** — toggleable overlay showing explored area and waypoints.
- **Inventory Panel** — per-character inventory + equipped slots. Drag-drop, weight readout, junk-flag, transfer between members.
- **Character Sheet** — attributes, derived stats, status effects, equipped items, biography. Often combined as tabs with Skills.
- **Skills Panel** — categorized skill list (Combat / General / Knowledge), level-up spending, perk display. Tabbed sub-panels for each category. Buttons confirmed in mod code: `OnCombatSkillsClicked`, `OnKnowledgeSkillsClicked`, `OnGeneralSkillsClicked` on `CHA_SkillPanel`.
- **Quest Log / Journal** — active and completed quests, journal entries from NPCs and discoveries. Often tabbed with achievements/codex.
- **Dialog Screen** — speaker portrait, text panel, keyword list, freeform input box, exit. Skill-check options inline.
- **Trade / Barter Panel** — two-pane vendor/player exchange, balance tracker, "sell junk" shortcut.
- **Loot / Container Panel** — shared list (party side) vs container side, take-all, take-individual.
- **Radio Screen** — incoming calls, mission briefings from General Vargas.
- **Pause Menu (in-game)** — Resume / Save / Load / Options / Main Menu / Quit.

### Modal popups (overlays)
- **TUT_TutorialPopup** — standalone in-game tutorial popup. Freezes input via `InputManager.SetFreezeInput()`. Managed by `TutorialScreen` singleton.
- **TutorialPopupMenu** — a `GUIScreen`-derived popup added to GUIManager screen stack. Used by character creation. Multiple can stack.
- **ModalMessageMenu** — generic confirm/cancel/info dialogs (save overwrite confirm, quit confirm, story choice confirm, etc.).
- **Level-up Notification** — appears on XP threshold; routes into Character Sheet/Skills.
- **Combat Start / End** banners — short transition overlays.
- **Death / Game Over** — special end state.
- **Dropdown / UIPopupList** — inline dropdowns inside other panels (Options, Character Creation). The mod patches `UIPopupList.Highlight` to read items.

### Mod state router priorities (from CLAUDE.md gotchas)
- DialogState (priority 70) — owns dialogue/tutorial popup announcements.
- GenericMenuState (priority 55) — generic menu screens, but explicitly excludes itself when CharacterScreen is active.
- CharacterState (priority 50) — character creation flow; queues with `Speak()` (not interrupt) when DialogState is also speaking.

## 8. Controls

### Vanilla mouse + keyboard
- **Mouse**: left-click selects/moves, right-click contextual (look at, attack, free-aim). Mouse wheel zoom; middle-drag rotates camera.
- **Hotkeys (default, partial — full list at orcz.com)**:
  - `1`–`7` — select party member 1–7.
  - `0` — select all rangers.
  - `Tab` — cycle next ranger.
  - `Space` — end turn (combat).
  - `M` — open world map / local map.
  - `I` — inventory.
  - `C` or `K` — character sheet / skills (varies by build).
  - `J` or `L` — journal / quest log.
  - `R` — reload.
  - `X` — swap weapon.
  - `V` — ambush / overwatch.
  - `Z` — crouch / toggle stance.
  - `Esc` — cancel / open pause menu.
  - `Enter` — confirm.
  - `F5` / `F9` — quicksave / quickload (in some builds; `F9` save / `F11` load reported on others).
  - `F1`–`F4` — sometimes mapped to skill quick-use slots.

### Director's Cut controller scheme (Xbox 360/One, PS4, Steam Controller — native)
This is the navigation model the accessibility mod most likely rides on, since DC's controller layer abstracts the menus into a focus/selection model rather than free-mouse hover.

- **Left stick** — move party (exploration) / move cursor (combat).
- **Right stick** — rotate / pan camera.
- **D-pad** — discrete cursor moves; menu navigation; sometimes ambush/crouch/zoom (per Steam Controller config: D-pad up/down = zoom, left = crouch, right = ambush).
- **A / Cross** — confirm / interact / end turn.
- **B / Circle** — cancel / back.
- **X / Square** — context action (reload in combat, secondary in menus).
- **Y / Triangle** — open map.
- **LB / L1 / RB / R1** — cycle party member / cycle target / page tabs in panels.
- **LT / L2 / RT / R2** — typically zoom or radial menu open / aim mode toggle.
- **Start / Options** — pause menu.
- **Select / Touchpad / Back** — journal or radio.

(Exact mapping varies by build and is rebindable in Options → Controls. The above reflects DC defaults from Steam Controller community configs and PCGamingWiki.)

The DC focus-selection model means UI elements have a discrete "focused" element at any time. The mod's hooks on `UICamera.SetSelection` and `UICamera.Notify(OnSelect=true)` are exactly what fire on each focus change.

## 9. Save System

- **Manual slot saves** (named slots) plus **quicksave** and **autosave**. Multiple slots per playthrough.
- **Save file format**: per-save folder containing two files:
  - `<savename>.xml` — human-readable save data (rangers, inventory, world flags, quest state).
  - `<savename>.bin` — binary companion (likely scene/serialized blob and screenshots).
- Editing the XML directly is feasible (community save editor exists; user notes confirm XML editability).
- **Save location (Windows, DC version)**:
  - `%USERPROFILE%\Documents\My Games\Wasteland2DC\Save Games\`
  - User-confirmed actual path on this machine: `E:\Users\Rol\Doccuments\My Games\Wasteland2DC\` (note: `Doccuments` is the user's actual folder name on disk).
- Original (non-DC) Wasteland 2 used the same path tree without `DC` suffix.
- Microsoft Store / Game Pass version may use a different encrypted store.

## 10. Modding Context

### Modding scene
- Small but real. Centered on Nexus Mods (~80+ mods listed). Common categories:
  - Save editors (the canonical save editor on Nexus is a popular tool).
  - Balance tweaks (heavy weapons, shotguns, SMG damage curves).
  - Portrait packs and character cosmetics.
  - Localization fixes.
- inXile shipped some asset/MSON tooling (MSON = inXile's JSON variant) but never released a full SDK or scripting API. No Steam Workshop integration.
- No published modding API docs. Decompilation of `Assembly-CSharp.dll` (Unity's IL2CPP was not used here — this is plain Mono .NET 3.5) is the practical reference.

### What MelonLoader provides here vs alternatives
- **MelonLoader** is a Unity-targeting mod loader that injects into the Mono runtime, bootstrapping mod DLLs at startup with Harmony already loaded. For Wasteland 2 specifically:
  - Game uses Mono + .NET 3.5 (Unity 4.x), making MelonLoader's Mono path a clean fit (no IL2CPP shenanigans needed).
  - **Harmony** patches let the mod intercept any method in `Assembly-CSharp` at runtime without editing DLLs on disk — critical for non-destructive accessibility hooks.
  - MelonLoader's logging console (`Latest.log`) is the user's primary debugging surface.
- Alternatives considered/avoided:
  - **dnSpy patching** of `Assembly-CSharp.dll` — destructive, breaks integrity checks, harder to update.
  - **BepInEx** — also viable on Mono Unity, but the mod chose MelonLoader; both expose Harmony similarly.
  - **Direct Unity asset edits** — useless for behavior changes; only relevant for art/text swaps.
- The accessibility mod thus relies on:
  - Decompilation of `Assembly-CSharp.dll` (indexed at `D:\Claude\Wasteland 2\Decompiled Code Index.txt`) for class/method discovery.
  - Harmony postfix/prefix patches on UI classes (UICamera, UIPopupList, ModalMessageMenu, etc.).
  - The **Tolk** screen-reader bridge DLL placed in the game directory for NVDA/JAWS/SAPI output.

## Verification Gaps

Items the document infers, generalizes, or could not pin down. Surface as user clarifications when relevant:

- **Exact AP costs per weapon class** — only sniper-rifle (~7) and assault-rifle burst (≤8) confirmed; full table not pulled. Likely worth extracting from decompiled `WeaponData` / similar.
- **Skill book exact list** — confirmed "one per non-combat skill" rule; specific titles not enumerated.
- **Quirk full list (DC only)** — sampled five; complete count is ~14 but full list not enumerated here.
- **Background list** — known to exist; specific names not enumerated.
- **Equipment slot count** — confirmed two weapon slots, body armor, headgear; trinket/accessory slot count uncertain (one or two depending on character — verify against `CharacterEquipment` in decompiled code).
- **Inventory weight formulas** — confirmed STR-driven; exact carry capacity formula not retrieved.
- **Combat Speed / AP-per-meter formula** — described qualitatively; exact formula not retrieved.
- **Initiative formula** — `5 + AWA + SPD/2` reported by community guide; cross-check against decompiled `CombatInitiative` calculation.
- **Hotkey defaults vary between original and DC builds** — community sources disagree on a few keys (quicksave F5 vs F9; character sheet C vs K). Authoritative source is the in-game Options → Controls panel; should be verified by user once mod can read it.
- **Controller default mapping** — varies; the layout above is composited from Steam Controller community configs and may differ slightly from native Xbox layout. Worth dumping `InputManager` defaults from decompiled code for ground truth.
- **Reputation / faction tracking** — confirmed *not* a numeric scale (W3 introduced that); exact list of faction flags / hostility states not enumerated.
- **Special / hidden skill category** — referenced in some sources; unclear whether these are truly user-facing or purely script-internal. Decompiled `SkillType` enum will resolve this.
- **Field Medic categorization** — listed as General in some sources, Knowledge in others. UI placement should be confirmed against `CHA_SkillPanel` tabs.
- **Save file `.bin` contents** — XML is human-readable, but the role/content of the paired `.bin` (screenshot? scene state? encrypted blob?) was not verified.
- **TUT_TutorialPopup vs TutorialPopupMenu coverage** — CLAUDE.md notes these as the two tutorial systems; whether other tutorial-like overlays exist (combat tutorials, world-map first-visit tooltips) is unconfirmed.

---

Sources (primary):
- Wasteland 3 Wiki (Fandom) — "Wasteland 2 skills", "Wasteland 2 quirks", "Wasteland 2 perks", "Wasteland 2 combat", "Wasteland 2 companions", "Wasteland 2 map", "Wasteland 2 status effects", "Wasteland 2 Director's Cut", "Action Points", "Awareness", "Constitution", "Unconscious", "Radiation (Wasteland 2)".
- gamepressure.com Wasteland 2 guide — "Character creation", "Combat options", "Combat tactics", "Injuries and death", "Party management", "Exploration", "The interface", "Dialogues".
- inXile Support — "Save File Locations (PC)".
- Nexus Mods Wasteland 2 hub.
- PCGamingWiki — Wasteland 2.
- Steam Controller Database — Wasteland 2 DC config.
- Project CLAUDE.md and `Decompiled Code Index.txt` (already in this repo).
