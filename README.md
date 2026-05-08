# CloudMusic Playlist Search

CloudMusic Playlist Search is a small Windows tool built for my own NetEase Cloud Music workflow. It reads the local `playingList` file used by the official client, builds an in-memory snapshot of the current queue, and provides a fast local search experience for the songs that are already queued.

The current version focuses on reliability and low overhead instead of deep client integration. The app loads the active playlist from disk, normalizes song metadata, and supports quick matching by song title, artist name, and mixed multi-word queries. Searches are handled in memory, so even large queues stay responsive. Recent fixes also cover extra spaces, punctuation differences, and real-world artist/title edge cases.

This repository currently contains:

- a WPF desktop prototype
- a core search engine with automated tests
- playlist parsing infrastructure for NetEase Cloud Music on Windows
- debug configuration for local development in VS Code

The long-term goal is an overlay that follows the Cloud Music window and can jump directly to a song inside the current playlist. For now, this repository serves as the first functional baseline for that idea.

