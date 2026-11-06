# WebRPG

[繁體中文](README.md)

WebRPG is a 2D RPG prototype I built as a portfolio project. It shows how I develop a Unity game client, connect it to account APIs and a real-time multiplayer backend, and collaborate with AI throughout development.

## Features

- **Solo adventure**. Explore a multi-floor map, choose a route, fight enemies, and pick up skills and items along the way.
- **Multiplayer adventure**. Create or join a room and start a run once everyone is ready, with up to four players. The server manages room and adventure state and syncs it to clients over WebSocket. Players can chat in real time within their room.
- **Accounts**. Register and sign in with email, with an option to stay signed in. Account data is stored in PostgreSQL.

## How I work with AI

I define the scope, expected behavior, and acceptance criteria before using AI to help break down tasks, implement features, and review changes. I make the design decisions and verify the results in Unity or with server tests, depending on the feature.

For example, the multiplayer lobby specification defines how joining rooms, toggling readiness, and disconnecting should work. The WebSocket integration tests exercise the room flow. [Lobby specification (Traditional Chinese)](WebRPGDocument/MultiplayerLobby.md) · [WebSocket integration tests](WebRPGServer.Tests/Lobby/LobbyWebSocketTests.cs)

The repository also documents how AI reads project specs, backs up local changes, and verifies its work. [AGENTS.md (Traditional Chinese)](AGENTS.md)

## Tech stack and repository layout

| Project | Technologies and role |
| --- | --- |
| [WebRPG](WebRPG/) | Unity 6.3, C#, URP, and NativeWebSocket. Handles the game UI, player input, and network connections, with WebGL support. |
| [WebRPGServer](WebRPGServer/) | ASP.NET Core (.NET 9) and PostgreSQL. Provides HTTP APIs for accounts and WebSocket connections for multiplayer. |
| [WebRPGShared](WebRPGShared/) | .NET Standard 2.1 library for client and server message types. |
| [WebRPGServer.Tests](WebRPGServer.Tests/) | NUnit unit, component, and WebSocket integration tests. |

[Project documentation index (Traditional Chinese)](WebRPGDocument/Document%20Directory.md)
