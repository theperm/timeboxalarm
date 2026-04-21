# timeboxalarm

Windows desktop app to sound alarms at configurable, hour-synchronized intervals.

## Features

- 5 independently configurable alarms
- Interval per alarm in minutes
- Enable/disable each alarm independently
- Alarm sound selector using built-in Windows event sounds
- Alarm timing synchronized to the hour (e.g. `5, 10, 15...`)
- Minimize to system tray when closing with `X`
- Quit from in-app **Quit** button or tray icon **Quit** menu item

## Run

```bash
dotnet run --project TimeboxAlarm/TimeboxAlarm.csproj
```

## Build

```bash
dotnet build timeboxalarm.sln
```
