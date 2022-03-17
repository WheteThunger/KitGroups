## Features

- Adds players to Oxide groups when they redeem kits
- Integrates with Timed Permissions to allow revoking group membership after a set time

## Configuration

```json
{
  "DebugLevel": 0,
  "Kits": {
    "DiedOnce": {
      "Group": "FailedExperiment",
      "Duration": "1h"
    },
    "DiedTwice": {
      "Group": "Noob",
      "Duration": "1h"
    }
  }
}
```

- `DebugLevel` -- Set to 1 or 2 to log debug information.
- `Kits` -- Defines which kits you want to detect being redeemed.
  - `Group` -- The name of the Oxide group you want to add the player to when they redeem this kit.
  - `Duration` -- The duration you want the user to be in the group after redeeming this kit. Set to `""` to last until map wipe. Format: `"1d12h30m"`,
