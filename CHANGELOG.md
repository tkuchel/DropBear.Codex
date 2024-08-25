# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `DropBear.Codex.Hashing`: Added new `SHA256Hasher` implementation for secure hashing.
- `DropBear.Codex.Blazor`: Introduced `LongWaitProgressBar` component with customizable styles.
- `DropBear.Codex.Core`: Added `Result<T>` return type to standardize API responses across all libraries.

### Changed
- `DropBear.Codex.Serialization`: Updated `JsonSerializer` to handle circular references.

### Fixed
- `DropBear.Codex.Files`: Fixed an issue where file verification hashes were incorrectly calculated under certain conditions.
- `DropBear.Codex.Operation`: Corrected a bug in the advanced operation manager that caused recovery steps to be skipped.

### Deprecated
- `DropBear.Codex.Utilities`: Deprecated `LegacyStringHelper` in favor of the new `StringExtensions` class.

## [1.0.0] - 2024-08-24
### Added
- Initial release of `DropBear.Codex` libraries:
    - `DropBear.Codex.Core`
    - `DropBear.Codex.Encoding`
    - `DropBear.Codex.Files`
    - `DropBear.Codex.Hashing`
    - `DropBear.Codex.Operation`
    - `DropBear.Codex.Serialization`
    - `DropBear.Codex.StateManagement`
    - `DropBear.Codex.Utilities`
    - `DropBear.Codex.Validation`

### Fixed
- N/A (Initial release)

## [0.1.0] - 2024-07-01
### Added
- Setup initial structure for `DropBear.Codex` libraries.
- Created foundational projects and initial implementations.

