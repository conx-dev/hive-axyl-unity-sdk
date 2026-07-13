# Hive Axyl Unity SDK

Hive Axyl Unity SDK is a UPM package for game clients. It provides authentication, session persistence, notices, mailbox, payments, and platform service APIs over Hive Axyl platform services.

## Requirements

- Unity 2021.3 LTS or higher
- .NET Standard 2.1 compatible Unity runtime
- Unity Package Manager
- Supported targets: Standalone, Android, iOS, and WebGL

## Installation

Add the package from Git URL in Unity Package Manager:

```text
https://github.com/conx-dev/hive-axyl-unity-sdk.git#<VERSION>
```

Or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.hiveaxyl.sdk": "https://github.com/conx-dev/hive-axyl-unity-sdk.git#<VERSION>"
  }
}
```

Replace `<VERSION>` with a published SDK version.

## Initialize

Create a `HiveAxyl` client and initialize it once before calling domain APIs.

```csharp
using HiveAxyl.Sdk;
using UnityEngine;

public sealed class HiveAxylBootstrap : MonoBehaviour
{
    private HiveAxyl hive;

    private async void Start()
    {
        hive = HiveAxylSdk.CreateHiveAxyl(new HiveAxylConfig
        {
            ProjectId = "PROJECT_ID",
            ApiKey = "CLIENT_API_KEY",
            ClientVersion = Application.version
        });

        await hive.InitializeAsync();
    }
}
```

## Configuration

| Option | Required | Description |
| --- | --- | --- |
| `ProjectId` | Yes | Hive Axyl project ID. |
| `ApiKey` | Yes | Client API key issued for the project. |
| `GatewayUrl` | No | Discovery gateway URL. Empty values fall back to the SDK default gateway. |
| `ClientVersion` | No | Client version reported during discovery. Defaults to `Application.version`. |
| `Language` | No | Language tag used for localized platform content. |
| `Debug` | No | Enables SDK debug logging. |
| `PersistSession` | No | Stores session tokens in `PlayerPrefs` by default. Set to `false` for in-memory storage. |
| `Platform` | No | Overrides automatic platform mapping. |
| `TokenStorage` | No | Custom token storage implementation. |

Automatic platform mapping sends Android as `ANDROID`, iOS as `IOS`, WebGL as `WEB`, and Standalone or Editor as `DESKTOP`.

## Authentication

Fetch enabled login providers before showing login UI:

```csharp
var providers = await hive.Auth.GetLoginProvidersAsync();
```

Supported auth entry points:

- `hive.Auth.LoginAsGuestAsync(deviceId)`
- `hive.Auth.LoginWithGoogleAsync(idToken)`
- `hive.Auth.LoginWithGoogleDesktopAsync(clientId, clientSecret, port)`
- `hive.Auth.LoginWithFacebookDesktopAsync(port)`
- `hive.Auth.RestoreSessionAsync()`
- `hive.Auth.LogoutAsync()`
- `hive.Auth.CurrentPlayer()`

Direct provider login accepts OAuth tokens obtained by your game through platform provider SDKs and sends them to the Hive Axyl server for validation.

`LoginWithFacebookDesktopAsync()` is available for Standalone and Editor builds. It opens the system browser, receives the server callback through a `127.0.0.1` loopback listener, and completes login with a short-lived one-time code. Configure the Facebook App ID and App Secret only in the Hive Axyl console.

## Payments

Use `hive.Payment` for purchase status and grant polling helpers.

Store-specific purchase flows and receipt collection are owned by the game client or platform billing plugin. Server-side validation credentials must be configured in the Hive Axyl console and are not stored in the client SDK.

## Notices and Mailbox

After `InitializeAsync()`, the same client exposes:

- `hive.Notice` for active notices
- `hive.Mailbox` for player mailbox operations
- `hive.Payment` for payment grant status

## WebGL

WebGL builds are subject to browser CORS rules. The Hive Axyl gateway and domain endpoints must allow the game origin used by your WebGL build.

## Error Handling

Domain errors are surfaced as `HiveAxylException` subclasses. Branch on exception type instead of parsing messages.

```csharp
try
{
    Player player = await hive.Auth.LoginAsGuestAsync(deviceId);
}
catch (HiveAxylBannedException banned)
{
}
catch (HiveAxylMaintenanceException maintenance)
{
}
catch (HiveAxylException error)
{
}
```

## Release Policy

Use a fixed SDK version in production builds. UPM Git releases are immutable Git tags, so fixes are released as new versions.

## License and Support

Use of this SDK is governed by the Hive Axyl license or service agreement for your project. For support, contact your Hive Axyl representative or support channel.
