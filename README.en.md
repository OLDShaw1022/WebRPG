# WebRPG

[繁體中文](README.md)

Play in your browser ([Production](https://oldshaw-webrpg.com/) · [Test](https://test.oldshaw-webrpg.com/))

WebRPG is a 2D RPG prototype I built as a portfolio project. It shows how I develop a Unity game client, connect it to account APIs and a real-time multiplayer backend, and collaborate with AI throughout development.

## Features

- **Solo adventure**. Explore a multi-floor map, choose a route, fight enemies, and pick up skills and items along the way.
- **Multiplayer adventure**. Create or join a room and start a run once everyone is ready, with up to four players. The server manages room and adventure state and syncs it to clients over WebSocket. Players can chat in real time within their room.
- **Accounts**. Register and sign in with email, with an option to stay signed in. Account data is stored in PostgreSQL.
- **Shop top-ups**. Choose a top-up package, buy crystals through PayPal, and check your order status and crystal balance.

## How I work with AI

I define the scope, expected behavior, and acceptance criteria before using AI to help break down tasks, implement features, and review changes. I make the design decisions and verify the results in Unity or with server tests, depending on the feature.

For example, the multiplayer lobby specification defines how joining rooms, toggling readiness, and disconnecting should work. The WebSocket integration tests exercise the room flow. [Lobby specification (Traditional Chinese)](WebRPGDocument/MultiplayerLobby.md) · [WebSocket integration tests](WebRPGServer.Tests/Lobby/LobbyWebSocketTests.cs)

The repository also documents how AI reads project specs, backs up local changes, and verifies its work. [AGENTS.md (Traditional Chinese)](AGENTS.md)

## Tech stack and repository layout

| Area | Technologies and role |
| --- | --- |
| [Game client](WebRPG/) | Unity 6.3, C#, URP, and NativeWebSocket for the game client. Model–View–Presenter (MVP) puts the SOLID single-responsibility principle into practice. In combat, the Model handles the rules, the View renders the UI, and the Presenter coordinates player input. The WebGL build is hosted on Firebase Hosting. |
| [Game backend](WebRPGServer/) | ASP.NET Core (.NET 9) and PostgreSQL power the backend. Minimal APIs expose HTTP endpoints for registration and sign-in, and WebSocket syncs multiplayer state. The server runs on AWS EC2. |
| [Shared protocol](WebRPGShared/) | .NET Standard 2.1 library for client and server message types. |
| [Frontend tests](WebRPG/Assets/WebRPG/Tests/Editor/) | Unity Test Framework (Edit Mode) covers adventure and combat logic, UI, and multiplayer connection flows. |
| [Backend tests](WebRPGServer.Tests/) | NUnit unit, component, and WebSocket integration tests. |
| [CI/CD (GitHub Actions)](.github/workflows/) | Manually triggered workflows build and deploy the WebGL client to Firebase Hosting, and run backend tests and deploy the server to AWS EC2. |
| Production and test separation | The production environment serves the stable build for players. The test environment is used to verify new features and fixes. Each environment has its own WebGL site and game server. |
| DNS | Cloudflare manages DNS for the game site and server. |
| Payments | PayPal-hosted checkout and the Orders API handle order creation and payment capture. The backend verifies webhook signatures and checks the payment amount and currency. A PostgreSQL transaction updates the order, crystal balance, and wallet ledger together to prevent duplicate credits. The backend reverses the credit after a confirmed full refund or payment reversal. Testing uses PayPal Sandbox, and production can be configured to use PayPal Live. [Currency and shop specification (Traditional Chinese)](WebRPGDocument/CurrencyAndShop.md) |

[Project documentation index (Traditional Chinese)](WebRPGDocument/Document%20Directory.md)
