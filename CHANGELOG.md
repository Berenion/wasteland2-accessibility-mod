# Changelog

All notable changes to the Wasteland 2 Accessibility Mod will be documented in this file.

## [1.0.0] - 2025-12-06

### Added
- Initial release of the Wasteland 2 Accessibility Mod
- Sets Xbox controller as the default input method
- Harmony patches for `InxilePlayerPrefs.GetInt()` to intercept input mode defaults
- Support for both overloaded versions of GetInt (with and without default parameter)
- Comprehensive documentation (README, SETUP_GUIDE)
- Automated build script for easy compilation
- MelonLoader integration

### Features
- Non-intrusive default change - respects user preferences once set
- Automatic detection of first-time launch
- Console logging for debugging and verification
- Compatible with all controller types (Xbox, PS4, Steam Controller)

### Technical Details
- Uses MelonLoader 0.6.1+ mod framework
- Implements Harmony prefix patches
- Targets .NET Framework 3.5 (Unity 4.x compatibility)
- Minimal performance overhead

### Documentation
- Complete installation guide
- Build from source instructions
- Troubleshooting section
- Configuration options

## Future Enhancements (Planned)

### [1.1.0] - Planned
- Configuration file support for choosing default input method
- In-game mod configuration menu
- Support for per-user default preferences

### [1.2.0] - Planned
- Additional accessibility features:
  - Customizable UI scaling
  - Enhanced contrast modes
  - Colorblind-friendly palette options
  - Audio cues for UI navigation

### [2.0.0] - Planned
- Complete accessibility overhaul
- Voice navigation support
- Remappable controller layouts
- Touch control support for Steam Deck

## Notes

This mod is designed to improve accessibility for Wasteland 2 Director's Cut. We welcome feedback and suggestions for future improvements.
