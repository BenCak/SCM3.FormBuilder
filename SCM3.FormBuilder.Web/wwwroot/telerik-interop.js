// Telerik JavaScript interop stub for Blazor Server environments
// This prevents TelerikMediaQuery errors when external CDN resources are blocked

window.TelerikBlazor = window.TelerikBlazor || {};

// Stub for media query initialization - returns boolean as expected by Telerik
window.TelerikBlazor.initMediaQuery = function () {
  console.log("TelerikBlazor.initMediaQuery initialized (stub)");
  return true;
};

// Stub for any other required Telerik methods
window.TelerikBlazor.readyCallback = function () {
  console.log("TelerikBlazor ready callback (stub)");
  return true;
};
