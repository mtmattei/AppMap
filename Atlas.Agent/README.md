# Atlas.Agent

Publishes a running Uno Platform app's navigation to the [Atlas](https://github.com/mtmattei/Uno-Builds) viewer, so its screen graph lights up live as you click through the app.

Atlas.Agent only reports navigation; it never changes how the host app behaves, and it does nothing when the viewer isn't running.

## Use

Reference the package in **Debug builds only**:

```xml
<ItemGroup Condition="'$(Configuration)'=='Debug'">
  <PackageReference Include="Atlas.Agent" Version="0.1.0" />
</ItemGroup>
```

Start it once, after the host is built (the app must use Uno.Extensions Navigation — `IRouteNotifier` is required):

```csharp
Host = await builder.NavigateAsync<Shell>();

#if DEBUG
_ = Atlas.Agent.AtlasAgent.Start(Host.Services, "MyApp");
#endif
```

Run the Atlas viewer, then run your app. The map fills in as you navigate; declared routes flip to *observed* the first time they actually fire.

## How it connects

The agent connects out to the viewer on `127.0.0.1:9743` and streams one newline-delimited JSON message per route change. It retries quietly while the viewer is down, so start order doesn't matter.
