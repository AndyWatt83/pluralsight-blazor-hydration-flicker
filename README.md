# The flicker — Module 1 demo

A deliberately tiny Blazor Web App that makes the prerender → interactive
handoff visible on camera, for *Data Hydration & State Persistence in ASP.NET
Core 10 Blazor*.

One static page, one component that opts into interactivity, and three states
you flip between with **comment toggles** — nothing is typed or deleted on camera.

```bash
dotnet run     # http://localhost:5199
```

Verified on SDK **10.0.400** / ASP.NET Core runtime **10.0.11**.

## The demo: three states, two comment toggles

All three states live in [`Components/PostCard.razor`](Components/PostCard.razor)
permanently. Nothing is added or deleted on camera — you only flip comments,
and the verdict panel recognises each state on its own (red / amber / green).

The `if (Post is not null) return;` guard is in the code from the start and is
**inert** until step 3: nothing sets `Post` before initialization unless the
framework restores it. The attribute is the switch, which keeps the diff between
"broken" and "fixed" honest — one line.

### State 1 — the flicker (the repo's resting state)

Run as-is.

```
#   RENDERER   RAN ON       INSTANCE   ACTION             DATA TIMESTAMP
1   Static     the server   4ba18b     FETCHED from API   10:45:45.776
2   Server     the server   5948ac     FETCHED from API   10:45:47.585
```

Two fetches, two instances, **two timestamps**. Content → spinner → content.
Red verdict.

### State 2 — the wrong fix (toggle the `@rendermode` lines)

Comment line 1, uncomment the `prerender: false` line below it:

```razor
@* @rendermode InteractiveServer *@
@rendermode @(new InteractiveServerRenderMode(prerender: false))
```

```
1   Server     the server   582ac0     FETCHED from API   10:46:29.464
```

One fetch — and an amber verdict, because **nothing was prerendered**: curl the
page and the content isn't in the HTML. The duplication went away because the
server render was deleted, not because its work was kept. Right answer to "this
component can't render on the server"; wrong answer to "my data is fetched
twice."

### State 3 — the fix (revert the toggle, uncomment the attribute)

```razor
@rendermode InteractiveServer
```
```csharp
[PersistentState]
public BlogPost? Post { get; set; }
```

```
1   Static     the server   0604fc     FETCHED from API        10:47:19.649
2   Server     the server   a2e8ea     restored from payload   10:47:19.649
```

**The instance still changed. The timestamp did not.** The second run is still a
brand-new object — serialization is required precisely *because* there's no live
reference to hold onto — but the data came across inside the page, so there was
nothing left to do. Green verdict, and the payload is visible in view-source:
`<!--Blazor-Server-Component-State:CfDJ8...-->` (`CfDJ8` = Data Protection —
encrypted under Interactive Server, unlike WebAssembly's readable base64).

Hot reload note: a `@rendermode` change is a structural edit — restart the app
between states rather than relying on hot reload. `dotnet watch` will prompt.

## The badge

The page carries the **same badge component twice**. It has no render mode of
its own, so it inherits its parent's:

```
This page — never opted in   │ RendererInfo.Name Static │ runs on the server │ interactive no
PostCard   — opted in        │ RendererInfo.Name Server │ runs on the server │ interactive yes │ instance c036c9
```

The purple one never changes. The blue one flips in front of the viewer — and
its instance id changes with it, even in state 3. That's SSR-first shown rather
than asserted: static is the default, interactivity is one deliberate opt-in in
one findable place.

**The "runs on" column is identical in both.** Under Interactive Server *both*
lifecycle runs happen on the server; nothing moves to the browser. A learner
arriving with React/Next hydration instincts will assume the flicker is
execution relocating client-side, and that's wrong here. Under
`InteractiveWebAssembly` row 2 would read `WebAssembly / your browser`; under
`InteractiveAuto` it's Server first visit, WebAssembly after. The badge handles
all three — only the Server path is exercised here.

## Recording notes

- **Use the Reload button, not enhanced navigation.** Internal Blazor
  navigations don't prerender, so only a full page load reproduces the handoff.
  The button calls `Nav.Refresh(forceReload: true)` for exactly that reason.
- **The panel is a fixed 190px**, so loading and loaded are the same height and
  the flicker reads as a content swap rather than the page jumping.
- **Dial the latency** in [`Services/SlowApi.cs`](Services/SlowApi.cs) →
  `Latency`. 1500ms narrates comfortably; ~400ms shows how easily this hides on
  a fast connection — a good beat for "invisible in dev, obvious in prod".
- **The trace self-resets** on every prerender, so the table always reflects
  exactly one page load. No stale rows between takes. (One presenter assumed;
  two browsers at once will interleave.)
- **`ReconnectModal` is deleted** from the template — circuit reconnection is
  deferred to the later resilience course and shouldn't appear on camera here.
- **The verdict panel is adaptive**: red for >1 fetch, green for 1 fetch once
  interactive. Flipping between states needs no other edits.
- **Nothing in state 1 is written badly.** Fetch in `OnInitializedAsync`,
  spinner while you wait — that's the idiomatic first component. Worth saying
  out loud: it isn't a mistake anyone made, it's the default.

## A note on the word "hydration"

Worth knowing before you say it on camera. "Hydration" is a real, decade-old
term — from the **JavaScript SPA world** (React ships `hydrateRoot()`), where it
means the client reconstructing component state and attaching handlers to
server-rendered HTML already in the DOM. Nobody at Pluralsight invented it.

But it is **not Microsoft's Blazor vocabulary**. Checked on 10.0.11:

| Source | `hydrat*` |
|---|---|
| `blazor.web.js` (Blazor's own runtime JS) | 0 |
| Entire ASP.NET Core 10 API doc surface | 1 |
| MS Learn *Prerender Razor components* | 0 |
| MS Learn *Prerendered state persistence* (~3,000 words) | 0 |

The single API hit is `CircuitHandler.OnCircuitClosedAsync` — about circuit
eviction, which this course explicitly defers.

Microsoft's verbs are **persist → restore**: *"Persist prerendered state"*, *"the
state is restored when the component renders interactively"*. They do use
**"flicker"**, twice, unprompted.

Two practical consequences:

1. A learner who searches "Blazor hydration" on MS Learn **will not find this
   topic**. The page they need is *"ASP.NET Core Blazor prerendered state
   persistence"*. Worth saying that mapping out loud, once.
2. In React/Next the data half is normally carried across **for you**. In Blazor
   it is not — you opt in per property with `[PersistentState]`. The borrowed
   term quietly predicts the wrong behaviour, and that expectation gap *is* the
   double fetch.
