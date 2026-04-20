# timeboxalarm

Windows desktop app to sound alarms at configurable, hour-synchronized intervals.

## Features

- 5 independently configurable alarms
- Interval per alarm in minutes
- Enable/disable each alarm independently
- Alarm timing synchronized to the hour (e.g. `5, 10, 15...`)
- Minimize to system tray when closing with `X`
- Quit from in-app **Quit** button or tray icon **Quit** menu item

## Run

```bash
dotnet run --project TimeboxAlarm/TimeboxAlarm.csproj
```

## Dev Container

This repository includes a dev container configuration in `.devcontainer/devcontainer.json`.

Open the repository in VS Code and use **Dev Containers: Reopen in Container** to develop in a consistent .NET SDK environment.

After the container starts, verify the project builds with:

```bash
dotnet build TimeboxAlarm/TimeboxAlarm.csproj
```
