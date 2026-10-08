# Contributing Guidelines

Contributions to this package are most welcome!

## Branches

Each Umbraco major has its own branch: `v17`, `v18` and `v19`. Make fixes on the lowest branch they apply to, then bring them forward to the newer branches.

## Test site

There is a test site in the solution to make working with this repository easier.
It is configured to do an unattended install; the login details are in `appsettings.Development.json`.

Put your Azure AI Search details in user secrets, never in `appsettings.json`:

```bash
cd src/Umbraco.Community.Search.Provider.AzureAI.TestSite
dotnet user-secrets set "AzureSearchProvider:Endpoint" "https://<service>.search.windows.net"
dotnet user-secrets set "AzureSearchProvider:ApiKey" "<admin api key>"
```

Generate demo content with `POST /api/seed/generate?count=200`. See the README for the other test endpoints.

### Switching branches

The test site's database (`umbraco/Data`) is not tracked by git, so it stays put when you switch branch.
An older Umbraco cannot open a database a newer one has upgraded. After switching to a different Umbraco major:

- move `umbraco/Data` aside (e.g. to `umbraco/Data.v18`, which git ignores) or delete it, and let the site install again
- delete the `bin` and `obj` folders, so the build doesn't run binaries from the previous branch

Alternatively, keep a separate working folder per branch with `git worktree add ../azureai-v18 v18`.

The unattended install writes an `Imaging:HMACSecretKey` into the test site's `appsettings.json`. Don't commit it.

## Tests

```bash
dotnet test src/Umbraco.Community.Search.Provider.AzureAI.Tests
```

Please add or update unit tests with your change.
