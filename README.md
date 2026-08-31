# The flicker — M1C1 demo

A deliberately tiny Blazor Web App whose only job is to make the prerender →
interactive handoff visible on camera, for **M1C1** of *Data Hydration & State
Persistence in ASP.NET Core 10 Blazor*.

One page. One component that opts into interactivity. No fix — the fix is M2.

```bash
dotnet run     # http://localhost:5199
```

Verified on SDK **10.0.400** / ASP.NET Core runtime **10.0.11**.

## The one-glance story

The page carries the **same badge component twice**. It has no render mode of
its own, so it inherits whatever its parent is running as:

```
This page — never opted in   │ RendererInfo.Name Static │ runs on the server │ interactive no
PostCard   — opted in        │ RendererInfo.Name Server │ runs on the server │ interactive yes
```

The purple one never changes. The blue one starts `Static / no` and flips to
`Server / yes` in front of the viewer. That's SSR-first shown rather than
asserted: static is the default, and interactivity is one deliberate opt-in in
one findable place.

**Note the third column is identical in both.** That is the most important thing
on the page.

## Where code runs vs. which renderer

The badge and the trace table state the renderer name and the **location**
separately, on purpose.

`RendererInfo.Name` returns `Static`, then `Server`. Neither of those is a
*place*. Under Interactive Server **both lifecycle runs happen on the server** —
nothing moves to the browser. A learner arriving with React/Next hydration
instincts will assume the flicker is caused by execution relocating client-side,
and that assumption is wrong here. The demo closes that door by saying "runs on:
the server" twice.

Under `InteractiveWebAssembly` the second row would read `WebAssembly / your
browser`; under `InteractiveAuto` it's Server on the first visit and WebAssembly
after that. Same boundary, same lost work, three different answers to "where".
(The badge already handles all three — only the Server path is exercised here.)

## What the trace proves

```
#   RENDERERINFO.NAME   RAN ON       COMPONENT INSTANCE   AT
1   Static              the server   fc16ce               17:01:33.247
2   Server              the server   8457ef               17:01:34.876
```

Two fetches, one page load, **two different component instances**. The instance
column is why "just hold onto the data" isn't available as a fix: the second run
isn't the same object waking up, it's a new one starting from scratch. That sets
up M2 — the state has to be *serialized* across, because there's no live
reference to carry.

## The beat sheet

1. **Land on the page.** Content is already there — the server fetched it and
   rendered it into the HTML. Blue badge reads `Static / no`.
2. **~1.5s later the panel goes amber.** Content gone, spinner in its place,
   badge flips to `Server / yes`. This is the moment the clip exists for.
3. **Content returns** — identical text, *different* timestamp.
4. **Point at the trace.** Two rows, two instances, both on the server.
5. **Click Like** to show interactivity has genuinely arrived, then **Reload and
   watch again**.

## Recording notes

- **Use the Reload button, not enhanced navigation.** Internal Blazor
  navigations don't prerender, so only a full page load reproduces the handoff.
  The button calls `Nav.Refresh(forceReload: true)` for exactly that reason.
- **The panel is a fixed 190px.** Loading and loaded states are the same height,
  so the flicker reads as a clean content swap, not the page jumping.
- **Dial the latency** in [`Services/SlowApi.cs`](Services/SlowApi.cs) →
  `Latency`. 1500ms is set for narrating over; ~400ms shows how easily this
  hides on a fast connection — a good beat for "invisible in dev, obvious in
  prod".
- **The trace self-resets.** Any prerender clears it, so the table always shows
  exactly one page load. No stale rows between takes. (Assumes one presenter;
  two browsers at once will interleave.)
- **`ReconnectModal` has been deleted** from the template. Circuit reconnection
  is deferred to *Advanced Blazor Rendering, State, and Resilience Patterns*, and
  a "Rejoining the server…" dialog appearing mid-take would put an out-of-scope
  topic on camera.
- **Nothing here is written badly.** Fetch in `OnInitializedAsync`, spinner while
  you wait — that's the idiomatic first component. Worth saying out loud: this
  isn't a mistake anyone made, it's the default.

## A note on the word "hydration"

Worth knowing before you say it on camera. "Hydration" is a real, decade-old
term — from the **JavaScript SPA world** (React ships `hydrateRoot()`), where it
means the client reconstructing component state and attaching handlers to
server-rendered HTML that's already in the DOM. Nobody at Pluralsight invented
it.

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
**"flicker"**, twice, unprompted — so the demo's framing is on-vocabulary even
where the course title isn't.

Two practical consequences:

1. A learner who searches "Blazor hydration" on MS Learn **will not find this
   topic**. The page they need is *"ASP.NET Core Blazor prerendered state
   persistence"*. Worth saying that mapping out loud, once.
2. In React/Next the data half is normally carried across **for you**. In Blazor
   it is not — you opt in per property with `[PersistentState]`. So the borrowed
   term quietly predicts the wrong behaviour, and that expectation gap *is* the
   double fetch. Used deliberately, that's the best hook in the module.
