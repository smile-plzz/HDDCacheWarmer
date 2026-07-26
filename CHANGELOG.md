# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]
### Added
- Initial codebase for HDDCacheWarmer:
  - `HDDCacheWarmer.Core` (engine, sequential reader, walker)
  - `HDDCacheWarmer.App` (WPF UI, settings, Explorer integration)
  - `installer` skeleton

### Fixed
- Fixed compilation errors in WPF App due to namespace conflicts (e.g. `Application` and `MessageBox`) arising from mixing WPF and Windows Forms.
- Added missing namespace `System.IO` usage in `MainWindow.xaml.cs`.
