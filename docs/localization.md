# Desktop languages

Desktop supports English, Simplified Chinese and the Windows display-language default. Choose a language in Settings and save preferences to keep it for the next launch. Changes are previewed immediately; discarding the unsaved preferences restores the saved choice. Existing preferences that do not contain a language setting follow Windows.

Desktop-owned labels, dialogs and common status/error messages use the shared localization catalog. Dynamic identifiers and paths are preserved. Core responses, logs, release notes, system errors, JSON/YAML and user-supplied data are shown as returned and may have a different language. Desktop does not change Core or WebUI language settings.

For an additional Desktop language, add its stable language value and name to the Settings selector, normalize the value in `LocalizationService`, and provide corresponding catalog entries and dynamic message formatting. Keep stored values and filter keys language independent, and preserve placeholders and any user or server data in translations.
