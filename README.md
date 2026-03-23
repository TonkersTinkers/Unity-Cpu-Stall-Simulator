# CPU Stall Simulator

Small Unity Editor-only stress tool for faking a slow PC.

## What it does

Combines:
- main thread freezes,
- frame chugging,
- worker thread CPU load,
- overlay display,
- temporary VSync and frame rate overrides.

## Use

Add it to a GameObject, enter Play Mode, then use:
- `Kick It Off`,
- `One Run Now`,
- `Stop It`.

## Main settings

### Run
`Auto Kickoff`, `Loop Forevver`, `Run Count`, `Start Delay`, `Wait Between Runs`

### Load
`Freeze Main Thread`, `Freeze Seconds`, `Chug Frames`, `Chug Seconds`, `Busy Ms Per Frame`, `Run Backround Load`, `Worker Count`, `Worker Load`

### Display + env
`Overlay Text`, `Overlay Font Size`, `Overlay Bg Alpha`, `Force VSync Off`, `Override Frame Rate`, `Test Frame Rate`

## Notes

Editor only, wrapped in `#if UNITY_EDITOR`.

Can freeze the Editor hard if values are high.
