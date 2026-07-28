# Common Asset Preview Server

## Goal

Allow artists on the same trusted LAN to inspect the Common Prefab and Texture assets that the PSD importer currently resolves. The feature is read-only and is controlled from the PSD Layout Tool global settings window.

## Scope

- Add a user-editable port field to the project settings asset. The initial value is 52342, but the editor never chooses or rewrites a port automatically.
- Add a Local Resource Preview Server panel to the global settings UI Toolkit editor window.
- Show stopped, starting, running, and error states with start, stop, and open-in-browser actions.
- Bind the server to all local network interfaces so trusted LAN clients can use `http://<editor-machine-ip>:<port>/`.
- Serve a bundled HTML page and a JSON catalog generated from `PsdCommonAssetCatalog`.
- Show Common Prefab and Texture names, asset paths, sizes, search, filtering, and a copy-name action.
- Show texture thumbnails by serving only cataloged image source files.

## Data And Sizes

The preview service uses `PsdCommonAssetCatalog` as the single source of truth. It refreshes the catalog before starting if it is missing or stale, then creates an immutable snapshot on Unity's main thread.

- Textures show sprite pixel dimensions and source file byte size.
- Prefabs show source file byte size. If the prefab root has a `RectTransform`, its design width and height are also shown.
- Browser requests never call `AssetDatabase` or any Unity object API.

## Server

Use a small `TcpListener` HTTP implementation rather than `HttpListener`. This avoids URL ACL and administrator requirements on Windows. The listener accepts only `GET` and serves:

- `/` - embedded HTML, CSS, and JavaScript
- `/api/catalog` - immutable JSON catalog snapshot
- `/asset/<id>` - a catalog-approved texture source file

The service does not expose arbitrary paths, directory listing, import, deletion, or write APIs. It is intended for a trusted LAN; no password or authentication is added in this scope.

## Lifecycle

- Validate the user-entered port before starting.
- Surface occupied-port and listener errors in the global settings panel.
- Stop on explicit action, editor shutdown, and assembly reload.
- Refresh the catalog snapshot when the Common catalog changes while the server is running.

## Verification

- Unit-test port validation and catalog snapshot construction without opening a socket.
- Compile in Unity.
- Start the server through the settings entry point, request the HTML and JSON endpoints, and capture the global settings window.
- Confirm a texture endpoint returns only a cataloged file and rejects an unknown identifier.
