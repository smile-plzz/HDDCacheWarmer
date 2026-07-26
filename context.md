# Project Context

## Goals
- Warm the OS file cache for a specified directory or drive by sequentially reading every file.
- Support accurate progression monitoring: scan phase for counting total files/bytes, followed by warming phase.
- Provide a responsive UI supporting pause, resume, and cancellation.
- Keep system resource usage constrained (e.g. read files in configurable buffer sizes, never loading entire files into memory).
- Integrate with Windows Explorer via HKCU context menu without requiring administrator privileges.

## Stack
- **Languages/Frameworks**: C#, .NET 8.0
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Target OS**: Windows 10/11
- **IDE/Build Tools**: MSBuild, .NET CLI (`dotnet build`), Visual Studio 2022

## Constraints
- Must run as normal user (no admin rights needed).
- Must avoid loading whole files into memory to prevent OOM errors on large files (e.g., >100 GB).
- Per-file error resilience: handle access or disk errors per file and continue the process.
