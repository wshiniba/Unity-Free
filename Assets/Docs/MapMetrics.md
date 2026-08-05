# Map Metrics

This guideline defines the working scale for `Assets/Scenes/fight.unity`.

## Baseline

- Reference prefab: `Assets/Prefab/Player 2.prefab`
- Player physical collider: `CapsuleCollider2D`, vertical
- Player collision width: about `1.17` Unity units
- Player collision height: about `3.45` Unity units
- Scale unit: `1 player height = 3.45 Unity units`

Use the physical collider height as the primary gameplay metric. The sprite is wider than the collision body, so sprite bounds should not drive tunnel, platform, or arena clearance.

## Traversal Clearances

| Use case | Recommended size |
| --- | ---: |
| Minimum passable tunnel height | `4.0` units |
| Comfortable room height | `5.0 - 6.0` units |
| Rope swing lane height | `8.0 - 14.0` units |
| Vertical shaft width | `5.0 - 8.0` units |
| Vertical shaft segment height | `10.0 - 18.0` units |
| Wide gap for rope traversal | `8.0 - 16.0` units |
| High-speed landing buffer | `5.0 - 8.0` units |

Keep rope routes more open than normal platformer routes. The player accelerates quickly, so narrow corridors should be used as pacing gates rather than as the default layout.

## Platforms

| Platform type | Width | Thickness |
| --- | ---: | ---: |
| Small foothold | `2.5 - 3.5` units | `0.5 - 0.8` units |
| Normal standing platform | `3.0 - 4.0` units minimum | `0.5 - 1.0` units |
| Combat platform | `8.0 - 14.0` units | `0.75 - 1.25` units |
| Arena floor segment | `18.0 - 32.0` units | `1.0 - 2.0` units |

Collision platforms should read clearly against decoration. In `fight.unity`, put gameplay collision under `/Map/PlatForm` and visual-only set dressing under `/Map/Background Decoration`, `/Map/PlatForm Decoration`, or `/Map/Front Decoration`.

## Rope Anchors

| Anchor use | Recommended spacing |
| --- | ---: |
| Basic ceiling anchors | `6.0 - 10.0` units apart |
| Alternating wall anchors | `7.0 - 12.0` units apart |
| Vertical shaft anchors | every `8.0 - 12.0` units of climb |
| Boss arena side anchors | `6.0 - 9.0` units above floor |
| Boss arena ceiling anchors | `10.0 - 14.0` units above floor |

Use `/Map/Drag` for hookable traversal points and boss-specific anchor objects under `/BossArena` when the anchors are part of combat. Anchor placement should support diagonal swing arcs, not just straight pulls.

## Boss Arena

| Arena element | Recommended size |
| --- | ---: |
| Minimum arena width | `28.0` units |
| Comfortable arena width | `36.0 - 48.0` units |
| Minimum arena height | `14.0` units |
| Comfortable arena height | `18.0 - 24.0` units |
| Side lockdown wall height | `10.0 - 16.0` units |
| Secondary platform height | `4.0 - 8.0` units above floor |

Boss target scale:

- Small enemy height: `2.8 - 3.8` units
- Boss height: `6.0 - 9.0` units

Leave enough empty air above the boss for rope recovery, aerial attacks, and projectile avoidance. The arena should have at least three practical rope choices: one ceiling route and one route on each side.

## Layout Checklist

- Player can stand, jump, and turn around in all intended corridors.
- Any tunnel below `4.0` units high is treated as blocked or decorative.
- Every major gap has visible hook opportunities before the player commits.
- Swing lanes are free of foreground decoration that looks solid but is non-colliding.
- Combat platforms are at least `8.0` units wide unless they are deliberately temporary or risky.
- Boss arenas have horizontal floor space, vertical recovery space, and side/ceiling anchors.
- Collision objects and decoration objects stay separated in the hierarchy.

## Current Scene Notes

`fight.unity` currently uses these high-level roots:

- `/Player 2`
- `/Map`
- `/BossArena`
- `/BOSS`

Continue using `/Map/PlatForm` for readable collision, `/Map/Drag` for rope traversal anchors, and `/BossArena` for combat boundaries, spawn positions, and boss-specific hook points.
