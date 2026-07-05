# Reclamation — Use Cases & Scenarios

Concrete gameplay scenarios that guide design — *what it should feel like*, not specs.
Touchstones for tuning. Collected from the original fantasies and controls design docs.

## Gameplay fantasies

### Dogfight airbrake (Top Gun)
The *Top Gun: Maverick* airbrake beat (~41:15) — Maverick decelerates hard so his pursuer
overshoots and ends up in front of him — but here **you're** the one being chased, by a
flying alien you can't shake. You pull the airbrake, slip behind it, and fire. A target
feel for vehicle/flight combat: momentum-based maneuvers that reward timing over raw speed.

## Control-flow scenarios

Walkthroughs of the planned Build/Combat mode controls and the R-key synergy
(see [input](systems/input.md)). `[key]` = input pressed.

### 1 — Pure building session (no threats)
`[1]` pick Small Cube → `[Scroll]` rotate → `[LMB]` place → `[6]` Electric Furnace →
`[LMB]` place by the generator → `[E]` open config, pick Iron Ingot → `[9]` Belt Tool →
`[LMB]` miner output → furnace input → `[8]` Wire Tool → `[LMB]` pole → generator.
*Scroll rotates without changing slot; Belt/Wire share the right-click cancel idiom; E works
whenever Build Mode's cursor is locked.*

### 2 — Pure combat engagement
`[1]` primary → `[Mouse]` aim → `[LMB]` fire → `[R tap]` reload → `[2]` secondary →
`[LMB]` clear → `[R hold]` holster → Build Mode.
*Tap R reloads (an accidental tap during a hold won't holster early); 1/2 swap without
holstering; hold-R is the only way back to Build — deliberate, prevents mid-fight mode flips.*

### 3 — The ambush *(core synergy)*
Deep in Build Mode, mid-belt, an enemy spawns. `[R]` instantly draws your last weapon (the
Belt Tool auto-cancels) → `[Mouse]` engage → `[R tap]` reload → enemy down → `[R hold]`
holster → `[9]` re-select Belt Tool → `[LMB]` resume.
*One keypress goes building → fighting; last-weapon memory means no weapon menu; heavy weapons
are excluded so R always feels fast; hold-R returns just as fluidly.*

### 4 — Base defence wave
`[E]` mid-recipe on an Assembler → `[E/Esc]` close (cursor re-locks) → `[R]` draw → `[LMB]`
engage from behind wall blocks → `[3]` rocket for an armoured enemy → `[LMB]` kill → `[2]`
back to secondary → `[R tap]` reload → wave clear → `[R hold]` holster → `[E]` confirm recipe.
*Switching to a heavy (3) mid-combat is fine — the R quick-draw exclusion only applies from
Build Mode, and heavies don't update the last-weapon memory, so R still holds the secondary.*
