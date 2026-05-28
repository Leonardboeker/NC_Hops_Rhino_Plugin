# Contributing to Wallaby Hop

Thanks for being curious about this project. Contributions and forks are welcome — with one small ask up front.

---

## Please tell me before you start

Wallaby Hop is licensed under [PolyForm Noncommercial 1.0.0](./LICENSE), which means anyone is free to use, fork, modify, and contribute for **noncommercial** purposes. I'm friendly about it — I just want to know who's using my work and what for.

**Before you fork, adapt, build on, or open a substantial PR, please [open a GitHub issue](https://github.com/Leonardboeker/NC_Hops_Rhino_Plugin/issues/new) describing:**

- What you're planning to do (use as-is, adapt for a different machine, extend a component, ship a fix)
- The context (hobby / academic / research / shop with paid jobs)
- Roughly when you're hoping to do it

Why I ask: it lets me flag gotchas that haven't made it into the docs, mention ongoing changes that might collide, or point you at related work. It also helps me understand who's actually using this — which directly shapes what I work on next.

This is **not** a gate or a slow approval process — a one-paragraph "hey, I'm planning to do X" is enough. I'll usually reply within a few days.

---

## What "noncommercial" means here

Per the PolyForm Noncommercial license, you can use Wallaby Hop for:

- Personal projects, hobby work, learning
- Academic and educational use (including coursework and thesis projects)
- Public research (including funded research at universities and public research institutions)
- Charitable, public-interest, or government work

You **cannot** use it for commercial purposes without a separate agreement. Commercial use includes (non-exhaustively):

- Embedding it in the toolchain of a paid woodworking shop's production workflow
- Selling a product that depends on it
- Offering it (or a derived service) for a fee
- Internal use at a for-profit company beyond evaluation

If any of that fits what you want to do, **please reach out** — I'm open to commercial licensing on reasonable terms, and the conversation is easier than you might expect.

---

## Pull Requests

PRs are welcome for:

- Bug fixes
- Documentation improvements (typos, clearer explanations, missing examples)
- New components that fit the existing architecture (please read [`DESIGN.md`](./DESIGN.md) first)
- Test coverage gaps
- Yak package / distribution improvements

For larger changes (new operation categories, breaking API changes, architectural shifts), **please open an issue first** so we can discuss scope before you put in serious work.

---

## Tests are the safety net

Before submitting a PR, run:

```bash
dotnet test
```

All 119 snapshot tests must pass. The snapshot tests guard the exact NC output format — a green test run means a tested machine program stays a tested machine program. If a test fails, the machine output would have changed too. Investigate before pushing.

---

## Code conventions

- Match the existing style (read a few `Hop*Component.cs` files before adding a new one)
- ASCII only in any string that ends up in a `.hop` file — the CAMPUS controller rejects Unicode
- Numeric formatting always via `CultureInfo.InvariantCulture` — German locales with `,` decimal separators would produce broken NC
- Component GUIDs **never change** — they're the long-term identity Grasshopper uses to wire saved `.gh` files. New component? New GUID. Renamed component? Same GUID.
- See [`DEVELOPMENT.md`](./DEVELOPMENT.md) for the build pipeline and the "add a new operation" walkthrough

---

## Reporting bugs

If you've hit a bug:

1. Check whether `dotnet test` passes against your local build — if a snapshot test is red, that's where to start
2. Open an issue with: Rhino version, plugin version, the minimal `.gh` file or operation that triggers it, expected vs actual NC output
3. If a CAMPUS controller is rejecting a generated `.hop`, paste the relevant `.hop` lines and the controller's error message

---

Thanks for reading this far. Happy machining.
