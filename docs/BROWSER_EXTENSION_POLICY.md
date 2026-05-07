# Optional browser extension policy

Browser extension support is not part of the default PetPresence MVP. If a user installs an extension later, it must be opt-in and local-first:

- Disabled by default.
- Classifies the active tab locally into broad categories only.
- Does not send raw tab titles, raw addresses, search queries, history, page text, or screenshots to the server.
- Sends only the same classified presence enum/status model used by the desktop detector.
- Can be disabled independently from desktop foreground detection.
