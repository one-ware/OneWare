---
sidebar_position: 2
id: changelog
title: ONE WARE Studio Changelog
sidebar_label:  Changelog
---

## 1.0.15

- Fix VCD Parsing cases
- Adjust ColorPicker Style
- Update Copilot Integration
- Fix Add Signal button in VCD Viewer
- Simplify Login Token Refresh Flow

## 1.0.14

- Update OSS CAD Suite
- Fix DLL Import Resolver for some plugins

## 1.0.13

- Fix Login to ONE WARE Cloud for MacOS

## 1.0.12

- Added device option for OpenVINO Onnx Runtime Provider
- Fix PackageService not loading if there is a broken value in custom sources setting
  
## 1.0.11

- Futher adjustments for new Auth System
  
## 1.0.10

- Prevent logout on disconnect
  
## 1.0.9

- Change Login flow (required to use OneWare Cloud)

## 1.0.8

- Fix Copilot Login issues
- Add Chat History Feature
- Improved Codec support for Linux OpenCV VideoWriter
- Improved handling when the IDE does not have access to the internet

## 1.0.7

- Improve OpenCV Runtime codec support
- Add optional ONNX Runtime bootstrapper and runtime packages
- Add cancellation to package installs
- PackageService additions and improvements

## 1.0.4

- Fix incomplete completionoffset
- Update clangd
- Increase default limit before cutting editing features in large files
  
## 1.0.3

- Styling adjustments for docking system
- Update OSS CAD SUITE

## 1.0.2

- Setting improvements
- Copilot Update + Handling improvements

## 1.0.1

- Copilot improvements and confirmation dialogues for sensitive actions
- Windows Terminal fixes
  
## 1.0.0

**This update breaks compatibility with existing plugins**

- New improved module loading system and dependency injection
- New Project System (huge performance improvements with big projects)
- AI Chat with integrated Copilot
- Search and Replace improvements
- Better global command system (CTRL + T)
- Setting Validation System+
- VCD Viewer improvements and bugfixes
- Added support for Windows ARM
- Terminal Integration fixes and improvements
- Unified Styles

## 0.21.28.0

**The next update (1.0) will be a big rework of the plugin system and breaks compatibility with existing updates**

- Minor Bugfixes and ToolService Integration
- Add command line arguments to gtkwave call
- Fix restart feature for Flatpaks
- Add possibility to show validation warnings for settings

## 0.21.27.0

- Add Linux Arm Version (Flatpak, Snap, .tar.gz)
- Add OpenCV runtime for linux with bundled FFMPEG (for mp4 input)

## 0.21.26.0

- Use Single Instance to open Files / URLs.

## 0.21.25.0

- Add option for automatic restart after Plugin Updates
- Add option to automatically install Plugins

## 0.21.24.0

- Allow login with browser
- Add Advanced Combobox (for title, value pairs)
- New optional email field for Feedback Tool

## 0.21.23.0

- Fix Camera Access for Linux (Snap and Flatpak)

## 0.21.22.0

- Fix Camera Access on MacOS
- Package Advanced runtime Libraries

## 0.21.21.0

- Make native dependency resolve for plugins more robust (fixes a possible issue on MacOS)
- Fix Output View sometimes showing wrong line colors
- Adjust URL open protocol as start option
- Add possibility to reset settings to default value

## 0.21.20.0

- Fix Regex VHDL Node Extractor parsing comments
- Fix OSSCadSuite Environment Variable (which caused NextPNR to not work properly)
- Implement Functionality to open OneWare from browser links oneware://...
  
## 0.21.19.0

- Revert PackageStatus change leading to incompatible Hardware Support Packages in OneAI Extension
  
## 0.21.18.0

- Fix upgrading to prerelease
- Add MacOS Code Signing

## 0.21.17.0

- Fix Opacity for Watermarks
- Add PackageWindowService
- Upgrade to .NET 10
- Fix folder deletion crash
- Simplify feedback form

## 0.21.16.0

- Add possibility to switch between node selectors
- Add autolaunch action for plugins
- Change compatibility requirment to first two digits of a version
- Add PCF File selection for Yosys
- Rename "New Project" to "New FPGA Project" for .fpgaproj format
  
## 0.21.15.0

- Fix Visual Issue in Project Explorer
- Fix Save and Compile not working in Pin Planner
- Keep files open on Project Reload
- Fix inconsistent Dropdown menu sizes in Package Manager Version selector
- Fix possible infinite loop when parsing port declaration using the Regex Method
- Fixed infinite loop (only occurred when typing ' IS  ' after adding line-break)
- [Windows] Improved startup time after first install using advanced code sining (because of windows defender)
- Fix updating logic when trying to update from prerelease version to stable

## 0.21.14.0

- Fix menu bar not always showing completely
- Add possibility to add prerelease packages/plugins
- Add environment variable and start argument to chose different path for default project folder

## 0.21.13.0

- Welcome Screen rework
  
## 0.21.12.0

- Improve Project Explorer Performance
- Fix Library Manager
- Add Feedback Dialog

## 0.21.11.0

- Fix critical error that would disable editor features

## 0.21.10.0

- Add Setting to increase character limit before disabling features for performance reasons

## 0.21.9.0
- fixed that 'CurrentDocument' is sometimes null after init
- Added a flyout next to the settings to display account information
- Add OssCadSuite Release 27.08.2025

## 0.21.8.0
- Update Packages
- Fix Window Closing Events

## 0.21.7.0

- Fix Slider Setting Validation
- Fix making space for hidden settings
- Update Splashscreen
- Add Commandline options helper
- Add new styles

## 0.21.6.0

- Fix VCD Viewer Bit parsing (thx to timonix)
- Prevent windows from being larger than the screen
- Cloud related changes, OneWare Studio no longer requires Git to use OneWare Cloud
  
## 0.21.5.0

- Fix window scaling issues

## 0.21.3.0

- Update Packages and Language Support
- Extended Connectivity with OneWare Cloud
- Small fixes and adjustments

## 0.21.2.0

- Allow plugins to load additional plugins
- Add project settings functionality

## 0.21.1.0

- Improve VCD Live View
- Switch RX and TX in USB Component
- Add VHDL Standard to Project Properties
- Fix a pissuble VCD Viewer crash when zooming in
- Make VCD Viewer Signals Selectable (needed for Extensions)
- Small bug fixes

## 0.21.0.0

- Upgrade to NET9
- New Plugin Compatibility System
- Various fixes and improvements
  
## 0.20.5.0

- Allow clicking on error messages (from GHDL) with control to jump to file
- Allow adding custom sources for Packages
- Add Library Explorer (needed for future updates)
- Add new GUI Element for FPGAs (pinBlock)

## 0.20.4.0

- Fix GTKWave not working from oss-cad-suite on Windows
- Fix one bug with indentation in VHDL
- Add "event" type to VCD Viewer

## 0.20.3.0

**There might be some issues on Windows after this update, caused by the installer. To resolve them, simply remove and re-install!**

- Fix VCD not showing small changes
- Fix VCD showing changes when value doesn't change
- Update VHDL support
- Fix some smaller issues
  
## 0.20.1.0

- Add more elements for FPGA GUI Creation
- Fix FPGA GUI for fractional scaling
  
## 0.20.0.0

**This update changes the way FPGA Dev Boards are loaded. As of now (9th August), only Max1000 and IceBreaker are available as packages, the others will follow in the coming days!**

- Add System that allows adding any FPGA Board by using JSON Files (with GUI!). **Documentation will follow**
- You can now zoom and move around with rightclick (or WASD/Arrow keys) in the Connect and Compile window!
- Add search combo box to some GUIs
- Fix some package incompatibility issues for future versions.
- Fix issue that caused the langauge server options not to be visible
- Fix SearchBox reset button

## 0.19.2.0

- Fix saving for custom device properties (Yosys/Quartus/...)
- Improve VCD Scale
- Improve Completion filtering
- Change Releases to Github Releases

## 0.19.1.0

- Fix Comboboxsetting binding error

## 0.19.0.0

**Due to changes with the plugin system, some plugins might need to be removed and reinstalled afer installing this update!**

- Update Plugin Base
- Delete files using the delete key
- Lots of small fixes and improvements (Avalonia Update)

## 0.18.5.0

- Fix terminal size on macos
- Ignore VT_VERSION Environment for terminal
- Improve scrolling in terminal
- Fix crash for linux terminals

## 0.18.4.0

- Fix possible terminal crash
- Change default terminal on MacOS zo zsh (if available)
- Open .GHW and .FST with GTKWave by default, if it is available
- Extensions will no longer lose their settings if they are incompatible because of IDE update

## 0.18.3.0

- Update base packages
- Add automatic package updates to IDE Updater system
- Fix snippets for some c++ completions
- Fix "update successful" notification showing every start

## 0.18.2.0

- Add snippet support for completion
- Added snippets for VHDL and Verilog completion
- Fix changelog not loading after update
- Fix completion getting out of sync with LSP

## 0.18.1.0

- Fix two possible crashes caused by invalid highlighting due to out of sync LS with editor
- Remove round bottom corners for main window in Windows 11

## 0.18.0.0

- VCD ignore entries without value change
  
## 0.17.7.0

**There was a bug with the builtin updater, make sure to manually download this update**

- Fix IDE Updater download path
- Fix VCD Viewer for files with large amount of signals
- Fix VCD Viewer vertical scrolling
- Add VCD Viewer horizontal scrolling with Mousewheel + Shift
- Added LSP Document Highlighting functionality
- Added LSP Inlay Hints functionality
- Added LSP support for semantic token highlighting
- Hide external errors on default (changeable in experimental settings)
- Improve control and click action for LSP (was broken on MacOS)
- Added binaries for linux, if snap is not available

## 0.17.6.0

- Update VHDL / Verible support
- Remove TOML Creator
- Fix FixedPointShift for negative Signed Integers in VCD
- Escape string produces by ASCII VCD lines
  
## 0.17.5.0

- Fix Signed Decimal Converter for VCD
- Fix Hex Converter for VCD
- Add ASCII Converter for VCD
- Add IDE Updater for Windows/MacOS

## 0.17.4.0

- Add VCD Viewer Horizontal Scrollbar
- Fix an issue where errors in child processes (GHDL, ...) would get ignored
  
## 0.17.3.0

- Hide Hover box when focus lost
- VCD Viewer now correctly shows all STD_LOGIC values in binary mode
- Improved VCD Rendering
- Improved VCD Parsing with multiple threads
  
## 0.17.2.0

- Hide Hover box when alt tabbing
- Update linux libraries

## 0.17.1.0

- Fix small version upgrades on windows
- Update OneWare.Essentials Plugin base to 0.5.0
- Update OneWare.UniversalFPGAProject Plugin base to 0.20.0
- Fix auto bracket
- Fix Completionwindow tooltip misplacement
  
## 0.17.0.4

- Fix open project dialog on Unix
- Allow selecting completion items by click
  
## 0.17.0.3

- Fix VCD Viewer error when loading small VCD Files with multiple threads
- Add VCD Viewer Datatypes
- Add Fixed Point Shift
  
## 0.17.0.2

- Add VCD Viewer support for REAL Type
- Fix VCD Viewer invalid value casting
- Adjust VCD Viewer styles
- Fix an VCD Viewer issue that hides small changes in a big timewindow
  
## 0.17.0.1

- Fix binary packages not setting environment variables at all times
- Add Max1000 16k, Max10 Ultra and Cyc5000
- Add more options to customize programming workflows
- No longer remove empty folders while renaming them

## 0.16.8.0

- Add Max10 and Max1000 to default FPGA list
- Add Long Term Programming
- Add device-conf for each device, allowing to customize Toolchain/Loader arguments
- Add button for VCD Viewer to add signals
- Minor Style changes

## 0.16.6.0

- Improve Package Manager System, allowing to directly update binaries such as GHDL
- Add VHDL-LS Package for OSX-ARM64
- Fix an issue with VCD Viewer reloading files after showing live simulation

## 0.16.4.0

- Fix Internal environment settings
- Fix VCD Parser not correctly showing values on all scopes

## 0.16.2.0

- Fix wrong VCD Path after Simulation
  
## 0.16.1.0

- Add Testbenches to Project System for easy GHDL/IVerilog settings
- Add Update Notifications for future versions
- Force child processes to close with studio
- Make VCDViewer correctly use $timescale in VCD Files
- Make VCDViewer controllable by keys (arrow keys for navigation and CTRL+Wheel for zoom)
- Make VCDViewer focus on set marker while zooming
- API Changes for easier plugin development

## 0.15.3.0

- Yosys/OpenFPGALoader fixes
- Added timer to tasks
- Bug fixes

## 0.15.0.0

- Lots of bug fixes
- Add Max10 Board
  
## 0.14.0.0

- Lots of bug fixes
  
## 0.13.1.0

- First Preview Release
