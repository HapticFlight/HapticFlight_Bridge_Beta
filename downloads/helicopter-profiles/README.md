# Helicopter profiles and references

> **Superseded for V2 engine users:** [download the V2 haptic-engine profiles here](../haptic-engine-v2/README.md). The v0.4 and UH-60 v1.9.0 profiles below use the older interface and are retained only for rollback. This folder's workbook describes that older interface.

## Downloads

- [Six-aircraft profile ZIP](Helicopter-Repaired-v0.4.zip?raw=true): AH-64D Apache, OH-58D Kiowa, AH-6J, MH-6J, UH-1H Huey and clean UH-60L / DAP.
- [Formatted telemetry and effects workbook](Aircraft-Telemetry-and-Effects-Formatted.xlsx?raw=true).
- [Literal module parameter names](Module-Parameter-Names.md).
- [Parameter source references CSV](Module-Parameter-Source-References.csv?raw=true).

Individual `.siprofile` files are also in this folder. Open a file and use **Download raw file** if your browser displays a preview. This repository is private, so sign into a GitHub account with repository access.

## Import

Extract the ZIP, import the appropriate `.siprofile` into SimHub ShakeIt Bass Shakers, and select it manually. Disable the previous profile and keep your existing sound-card/channel routing.

The five repaired v0.4 profiles contain 171 enabled effects. The clean UH-60 v1.9.0 contains 41 enabled effects, preserving its baseline formulas and tuning. Enabled effects still require their underlying telemetry. Offline checks passed; live DCS output and tactile calibration have not been confirmed in this task.

Read [repair notes](REPAIRED-v0.4.md) and [UH-60 clean-copy notes](CLEAN-v1.9.0.md). The module-name inventory covers readable Lua references; compiled internals and dynamically constructed names are not exhaustive.
