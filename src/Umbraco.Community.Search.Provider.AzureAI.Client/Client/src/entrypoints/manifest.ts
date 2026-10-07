export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Umbraco Community Search Provider Azure AIClient Entrypoint",
    alias: "Umbraco.Community.Search.Provider.AzureAI.Client.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint.js"),
  },
];
