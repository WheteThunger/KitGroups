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
      "Duration (minutes)": 60
    },
    "DiedTwice": {
      "Group": "Noob",
      "Duration (minutes)": 60
    }
  }
}
```

- `DebugLevel` -- Set to 1 or 2 to log debug information.
- `Kits` -- Defines which kits you want to detect being redeemed.
  - `Group` -- The name of the Oxide group you want to add the player to when they redeem this kit.
  - `Duration` -- The number of minutes you want the user to be in the group after redeeming this kit. Set to `0` to last until map wipe.
