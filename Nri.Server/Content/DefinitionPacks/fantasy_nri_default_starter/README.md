# Fantasy NRI Default Starter

This starter pack contains the first minimal Fantasy NRI Default core definitions for Definitions v2 dry-run validation.

Included categories:
- attributes
- derived_stats
- currencies
- skills
- development_nodes
- development_hexagons
- race_traits
- races
- subspecies
- hybrids
- hybrid_subtypes
- languages
- continents
- countries
- city_states
- equipment_slots
- items
- weapons
- armor
- ammo
- condition_groups
- conditions
- location_types
- regions
- locations
- factions
- organizations
- laws
- restrictions
- market_tags

Planned next categories:
- projects
- technologies

Planned next tasks:
- cross-reference validation
- dry-run/import command
- closing audit

The development nodes are a starter test set, not a final class catalog. They exist to support future Character v2 / DevelopmentProfile validation while `UseDevelopmentNodeModel` remains disabled by default.

The race definitions are starter metadata for future RaceOrSpeciesProfile and BodyProfile workflows. Hybrids are manually listed canonical options; there is no free automatic hybrid generation. Race modifiers are draft data only and are not applied to Character. Language hints are placeholders, not final LanguageDefinition records.

The language definitions cover the Western Continent / Egunsentilurra starter set. They are not connected to Character, and no LanguageProfile exists in this stage. Existing race and subspecies `languageHints` remain reference hints until future cross-reference validation and continent/country/city-state definitions land.

Egunsentilurra is the first working continent in the pack. Countries and city-states are lore/data definitions only; maps, economy simulation, faction engines, wars, markets and diplomacy mechanics are separate future stages.

Equipment definitions are starter metadata only. Weapon, armor and ammo values are draft data and are not connected to combat. Prices are draft data and are not connected to economy. The fantasy starter pack intentionally avoids gunpowder weapons; `rune_pistol_draft` is a draft magitech concept, not a normal firearm.

Condition definitions are starter metadata only. All effect hints are draft data and are not connected to Character, ConditionProfile, combat, magic or Fate Engine. ConditionProfile remains unchanged in this stage.

Location definitions are starter metadata only. They provide location types, regions and points of interest for the Western Continent / Egunsentilurra. Maps, travel, economy, faction control and world events are separate future stages. Some locations are marked `hidden_until_discovered` or `gm_only`; hidden mechanics are not stored in `ServerOnlyData`.

Faction, organization, law, restriction and market tag definitions are starter metadata only. Faction engines, economy engines, law enforcement, market simulation and character integration are separate future stages. Secret factions and organizations use `gm_only` or `hidden_until_discovered`; `ServerOnlyData` is intentionally empty.

Automatic import is disabled. Foundation 0.6 uses dry-run loading and validation first; this pack must not seed MongoDB or replace legacy handlers by itself.
