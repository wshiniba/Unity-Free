# Restart Work Log - 2026-06-13

This file preserves the current Unity project context for the next restart.

## Current Project State

- Project path: `D:\user\Free`
- Current branch: `four-rope`
- Remote tracking branch: `origin/four-rope`
- Important scene file: `Assets/Scenes/fight.unity`
- `fight.unity` exists on the `four-rope` branch.
- A previous confusion happened because the project was on `main`, where `fight.unity` is not present. Switching back to `four-rope` restored it.
- Current extra untracked file: `.vsconfig.main-backup`
  - This was created only as a safe backup while switching branches.
  - Its content matched the tracked `.vsconfig`.

## Unity MCP Notes

- Earlier in the session, Unity MCP connection was healthy and returned `pong`.
- MCP version reported: `5.5.2`.
- Unity Editor version path reported earlier: `D:/unity/unity Editor/6000.4.0f1/Editor/Unity.exe`.
- At the moment this log was written, one scene-list query returned `Unity connection not available`; reconnect/check MCP after restart if needed.

## Completed Context Review

- The project is a 2D side-scrolling action platformer with fast rope-swinging / ODM-style movement.
- Main gameplay focus:
  - Rope launch
  - Pulling
  - Swinging
  - Jumping
  - High-speed traversal
  - Directional player attacks
  - Boss arena combat
- Main visual direction:
  - Dark fantasy cave
  - Underground ruins
  - Green-gray atmospheric fog
  - Layered parallax cave background
  - Clear high-contrast foreground collision platforms

## Player Metric Reference

Use `Assets/Prefab/Player 2.prefab` as the current player metric baseline.

- Root scale: `1, 1, 1`
- Main collider: `CapsuleCollider2D`
- Capsule direction: vertical
- Physical collision body:
  - Width: about `1.171` Unity units
  - Height: about `3.448` Unity units
- Collider offset:
  - X: about `0.161`
  - Y: about `-0.029`
- Sprite visual bounds:
  - Width: about `3.0` Unity units
  - Height: about `3.625` Unity units
- Rigidbody2D:
  - Mass: `2`
  - Gravity Scale: `4`
  - Linear Damping: `0.49`

Important design decision:

- Use the physical collider height, not the sprite width, as the gameplay metric.
- Baseline: `1 player collision height = about 3.45 Unity units`.

## Suggested Map Metrics

- Minimum passable tunnel height: `4.0` units or more.
- Comfortable room height for normal movement: `5.0 - 6.0` units.
- Platform thickness: `0.5 - 1.0` units.
- Normal standing platform width: `3.0 - 4.0` units minimum.
- Combat platform width: `8.0 - 14.0` units.
- Small enemy height: `2.8 - 3.8` units.
- Boss height target: `6.0 - 9.0` units.

Because the player uses rope movement, maps should include:

- Vertical shafts
- Diagonal swing lanes
- Wide gaps
- Ceiling and wall anchor opportunities
- High and low routes
- Boss arenas with enough horizontal and vertical clearance

## Map Reference Prompt Context

Short description for image/reference generation:

```text
Dark fantasy 2D side-scrolling platformer level concept, designed for fast rope-swinging traversal, underground cave and ruined temple environment, layered parallax background, green-gray atmospheric fog, readable foreground platforms, vertical shafts, hookable anchor points, wide boss arena spaces, gameplay layout reference, no characters, no UI.
```

Reference image categories to generate:

- Basic cave level layout with multi-layer platforms and swing space.
- Vertical rope traversal shaft with anchor points on both sides.
- Large boss arena with top/side hook points, multiple platform heights, and lockdown entrances.
- Cave plus ancient ruin hybrid area with broken stone structures and clear traversal routes.

## Next Suggested Work

- Reconnect/check Unity MCP after restart.
- Confirm Unity is on branch `four-rope`.
- Open `Assets/Scenes/fight.unity` if it is not already active.
- Use the player metric baseline above to define:
  - map grid scale
  - platform sizes
  - building/ruin scale
  - enemy and boss scale
  - hook anchor spacing
- After map metric is decided, document it as a formal `MapMetrics` guideline in `Assets/Docs`.
