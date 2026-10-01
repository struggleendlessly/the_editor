using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using TreeEditor.Client;
using TreeEditor.Client.Api;
using TreeEditor.Client.Cache;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddMudServices();
builder.Services.AddSingleton(new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<TreeApi>();
builder.Services.AddSingleton<TreeCache>();

await builder.Build().RunAsync();
