# Atomix — Windows + itch.io + $4.99 Premium Unlock: Technical Blueprint

Status: **Planning document — no code has been written yet.** This is the reference to hand back to Claude for step-by-step implementation.

Primary reference: [`implementation_plan.md`](implementation_plan.md) (Plan 2: Windows Desktop → itch.io). This document extends that plan with the account/monetization/entitlement system and supersedes its "Strategy 1/2" monetization sketches with a concrete, verified architecture.

---

## 0. itch.io fact-check (done before any recommendation)

Verified against official itch.io docs and current itch.io developer-community reports (Sept 2026). This section separates **A) what itch.io officially supports**, **B) what real developers report actually works in practice**, and flags where the two disagree.

| Question | Answer |
|---|---|
| Does itch.io process payment for downloadable games? | Yes. itch.io supports Free / Paid / "Name your own price" (with optional minimum) via Stripe/PayPal. Itch takes **0% by default** — you choose the revenue share (10% suggested, adjustable to 0%). [itch.io Pricing docs](https://itch.io/docs/creators/pricing) |
| Can a downloaded Windows `.exe` reliably know "this running copy belongs to a user who paid"? | **No, not out of the box.** itch.io has no client-side "am I paid for" call that a bare desktop executable can trust with zero setup. Something (OAuth token, download key, or your own login) must first identify *who* is running the game, and that identity must be checked against itch.io's records **server-side**. |
| Can itch.io OAuth be used from a Unity desktop app? | **Partially, and unreliable in practice.** itch.io's OAuth doc ([itch.io/docs/api/oauth](https://itch.io/docs/api/oauth)) documents an Implicit flow (`https://itch.io/user/oauth`) with desktop-friendly redirect options (loopback `http://127.0.0.1:PORT` or out-of-band copy/paste). Documented scopes include `profile:me`, `profile:owned` (owned-keys), and — critically — **`game:view:ownership`**, which the docs describe as letting a third-party OAuth app check `GET https://api.itch.io/games/:game_id/ownership` → `{"owns": true/false}` for **games created by the OAuth app's own registering developer account**. This is, on paper, exactly the mechanism Atomix needs. **However**, multiple current developer forum threads ([oauth-check-license](https://itch.io/t/3496438/oauth-check-license), [let-third-party-oauth-apps...](https://itch.io/t/6502109/let-third-party-oauth-apps-request-the-scopes-needed-to-build-a-real-client), [game:view:purchases-seem-to-not-work](https://itch.io/t/3245637/itchs-oauth-gameviewpurchases-scope-seem-to-not-work)) report that itch.io in practice often **only approves the `profile:me` scope** for a newly registered third-party OAuth app, and that broader scopes are inconsistently granted/enforced. **Treat `game:view:ownership` as "documented but unverified for your app until you personally register it and test it."** Build the architecture so it does **not** depend on that scope being granted. |
| Can itch.io APIs be called safely from the client? | Only the ones scoped to *the logged-in player's own token* (`profile:me`, `profile:owned`, and — if granted — `game:view:ownership`) are safe client-side, because the token itself is the credential and is per-user. **Never** embed your own creator/account API key (the one with `game:view:purchases` scope) in the Unity build — this key can read *all* purchases/emails for your game and, being on every player's disk, would be trivially extracted (confirmed as a real concern in [GitHub itchio/itch.io#1121](https://github.com/itchio/itch.io/issues/1121), where a dev explicitly avoided shipping their API key for this reason). |
| Is there a reliable server-side verification mechanism? | **Yes — this is the one itch.io mechanism that is solid and official.** Your own backend (never the client) holds your secret creator API key and calls: `GET https://itch.io/api/1/{API_KEY}/game/{GAME_ID}/download_keys?download_key=...` or `?email=...` or `?user_id=...`. Returns the download-key record (owner, game, created_at) or `{"errors":["invalid download key"]}`. There's also `.../purchases` for raw purchase records. [Serverside API reference](https://github.com/itchio/itch.io/blob/master/docs/api/serverside.md), [Download keys docs](https://itch.io/docs/creators/download-keys). This requires you to already know the buyer's **email or download key** — i.e. you need the user to tell you one of those (either by pasting it, or via OAuth `profile:me`/`profile:owned` giving you their itch.io identity to cross-check). |
| Are itch.io "sub-products/DLC" appropriate? | itch.io has no first-class DLC object like Steam. The idiomatic pattern is either (a) multiple files in one project gated by "restrict to paying customers," or (b) **a second, separate itch.io project used purely as a purchasable SKU** (a "key"/"unlock" product with no meaningful file), which is a well-established itch.io community pattern for exactly this "free base + paid unlock" scenario. See §2. |
| Is an external account/entitlement server necessary? | **Yes.** Nothing in itch.io's toolset lets a Unity build alone (a) know who's playing, (b) verify a purchase against your account system, and (c) persist that permanently, reissue it after reinstall, and sync it across machines. That combination requires your own backend + database. This is not itch.io being deficient for your case specifically — no storefront (Steam included) hands a raw client a trustworthy "is paid" boolean without *some* server round trip; itch.io is just more manual about it than Steamworks. |
| What can the client safely trust on its own? | Nothing permanently. A cached, **cryptographically signed** entitlement blob issued by your server can be trusted offline (see §5) because forging it requires your server's private signing key, not just editing a file. A raw `PlayerPrefs` bool or unsigned JSON file can never be trusted. |

**A / B / C / D summary:**
- **A. itch.io officially supports:** hosting the Windows build for free/PWYW/paid, processing the $4.99 payment, issuing download keys, and a serverside API to verify a key/email against a specific game.
- **B. Unity can implement:** opening the system browser to the itch.io purchase page, an in-game "Verify Purchase" form (email + download key, or itch.io OAuth login for autofill), local caching of a signed entitlement, offline fallback, and gating scenes/UI.
- **C. Requires your own backend:** the actual "is this real" check (holds the secret itch.io API key), your account system (Supabase Auth + Postgres), issuing signed entitlement tokens, and syncing progress across machines.
- **D. Not realistically possible/secure:** a one-click, fully in-Unity credit-card checkout (itch.io has no client-side checkout API — payment must happen in a browser on itch.io's site); a purely client-side "call itch.io and get a trustworthy yes/no" with no account layer of your own; making the desktop build literally uncrackable (see §9 — that's true of every DRM scheme, not specific to itch.io).

---

## 1. Executive Recommendation

**Recommended architecture: Single Windows build (all 8 experiments included) + Supabase (Auth + Postgres + Edge Functions, free tier) as your account/entitlement backend + itch.io as storefront/payment/file-host only.**

- Do **not** build the primary purchase-verification path around itch.io OAuth `game:view:ownership` — it's the nicest possible UX if itch.io grants it to your app, so **attempt it as an optional fast-path**, but the system must work perfectly without it.
- The **guaranteed, always-works path** is: user buys on itch.io in a browser → itch.io emails/shows them a download key → user pastes their email + that download key into Atomix's "Verify Purchase" screen → your Supabase Edge Function checks it against itch.io's serverside API using your secret key → Edge Function marks `premium_unlocked = true` permanently on that user's Atomix account row → client receives and locally caches a signed entitlement.
- Atomix gets its own lightweight account system (Supabase Auth: email/password, magic link, or "Continue with itch.io" as a convenience login) so you have one durable `atomix_user_id` to attach entitlement and progress to — independent of which machine, itch.io purchase mechanics, or future platform (Quest/WebGL/Steam) the player uses.
- This is chosen over a Firebase-based backend because Supabase Edge Functions can make **outbound HTTPS calls to itch.io's API on the free tier with no billing account required**; Firebase Cloud Functions require the paid **Blaze** plan (a credit card on file) for any outbound network call at all, even to stay at $0 spend — a real obstacle to your "everything free" requirement. Full comparison in §4 and §15.

This satisfies every hard requirement you listed: one-time $4.99 unlocks all of 4–8 permanently, survives reinstall/new-PC via login, works offline after first verification, and never relies on `PlayerPrefs.SetInt` alone.

---

## 2. Recommended Product / itch.io Structure

**Two itch.io projects under your account, linked by your backend — not itch.io "DLC," and not one project with mixed free/paid files.**

1. **`Atomix` (main project)** — Kind: *Downloadable*. Pricing: **Free (or "Name your own price," $0 minimum)**. This is the one and only Windows build ZIP. It contains **all 8 experiments' code and assets** (see §9 for why shipping premium assets in the free build is an accepted, industry-normal tradeoff, not "no verification" — the verification is what's being added, not asset separation). Experiments 1–3 are unlocked by default; 4–8 are gated by the in-game `EntitlementManager`.
2. **`Atomix — Premium Unlock` (second project)** — Kind: *Downloadable* (itch.io requires a project to have a "file," even a nominal one — e.g. a one-page PDF "Thank you / how to activate your unlock" or a `.txt` receipt note). Pricing: **fixed $4.99, no PWYW**. This project exists purely to be a clean, unambiguous **purchase record** (its own `game_id`) that your backend queries — it is never installed or run. Buying it is what generates the itch.io download key you verify.

**Why two projects instead of one project with "restrict this file to paying customers":** that per-file restriction (`itch.io/docs/creators/access-control`) still ties the paid tier to *downloading a different file*, not to *unlocking content inside a build the user already has*. Since your requirement is "one running executable, gated in-app, unlockable without re-downloading," decoupling "get the game" (free project) from "buy the unlock" (paid project) is the cleanest mapping to a single verifiable `download_keys` lookup and avoids customers thinking they must pay before they can download anything at all.

**In-game "UNLOCK PREMIUM" button** does `Application.OpenURL("https://youraccount.itch.io/atomix-premium-unlock")` — itch.io handles the actual Stripe/PayPal checkout in the system browser (this is the only way; itch.io has no embeddable/headless checkout API). The user returns to the running game to complete verification.

---

## 3. System Architecture

```
                         ┌───────────────────────────────────────────┐
                         │                 itch.io                    │
                         │  - Atomix (free download, the .exe/.zip)   │
                         │  - Atomix Premium Unlock ($4.99 product)   │
                         │  - Payment processing (Stripe/PayPal)      │
                         │  - Serverside API (download_keys, purchases)│
                         └───────────────┬─────────────────────────────┘
                                         │  HTTPS, SECRET creator API key
                                         │  (server-to-server ONLY)
                                         ▼
┌───────────────────────────────────────────────────────────────────────┐
│                     Your Backend — Supabase (free tier)                │
│  ┌───────────────┐  ┌──────────────────┐  ┌───────────────────────┐   │
│  │  Supabase Auth │  │ Postgres (users,  │  │  Edge Functions        │   │
│  │  (email/pw,    │  │ entitlements,     │  │  - verify-purchase     │   │
│  │  magic link,   │  │ progress, coins)  │  │  - issue-entitlement-  │   │
│  │  itch.io OAuth)│  │  Row-Level Security│  │    token (signed)      │   │
│  └───────────────┘  └──────────────────┘  │  - sync-progress        │   │
│                                             └───────────────────────┘   │
└───────────────────────────────┬─────────────────────────────────────────┘
                                 │ HTTPS (Unity UnityWebRequest / REST)
                                 │ user's own Supabase JWT
                                 ▼
┌───────────────────────────────────────────────────────────────────────┐
│                     Atomix.exe (Windows, offline-capable)               │
│  AuthManager → EntitlementManager → ExperimentAccessManager → Scenes    │
│         │              │                                                │
│         │              └── caches SIGNED entitlement token locally      │
│         │                  (Application.persistentDataPath, NOT         │
│         │                   PlayerPrefs) → works fully offline after    │
│         │                   first successful verification               │
│         └── ProgressManager / SaveManager → syncs coins/history to      │
│             Postgres when online, local file is always source of truth │
│             when offline                                                │
│                                                                          │
│  Convai REST API — separate, unrelated integration; already requires    │
│  internet; unaffected by entitlement system except being itself one     │
│  more "premium/experiment-scoped" feature you can gate the same way     │
└───────────────────────────────────────────────────────────────────────┘
```

---

## 4. Authentication Architecture

**Recommendation: Supabase Auth, single source of identity, with "Continue with itch.io" as an optional convenience linked login.**

| Option | Windows desktop fit | Security | Effort | Cost | Entitlement linking | Offline | Privacy | Quest/WebGL later | Migration |
|---|---|---|---|---|---|---|---|---|---|
| itch.io-only auth | Poor — OAuth from a bare desktop app needs a local loopback listener; scopes unreliable (§0) | Medium | Medium-High (custom OAuth plumbing) | Free | Weak — itch.io identity ≠ your own durable user record | N/A alone | Depends on itch.io's policies, out of your control | Awkward — itch.io accounts don't exist off itch.io | Hard to leave later |
| Custom Atomix accounts (build your own auth server) | Good | You own all bugs | High | Server hosting cost | Full control | Good if designed for it | Full control | Fine | Fine, but you're maintaining crypto/session code yourself |
| **Firebase Auth** | Good | Strong (Google-managed) | Low | Free tier generous, **but Cloud Functions (needed to call itch.io API) require Blaze billing plan even at $0 usage** | Good, needs Cloud Functions for the itch.io check | Good | Google's privacy/ToS apply | Good SDK coverage | Vendor lock-in to Firebase/GCP data model |
| **Supabase Auth (recommended)** | Good | Strong (Postgres + RLS, JWT) | Low | Free tier generous; **Edge Functions call itch.io API with no billing account needed** | Good — one Postgres row per user, entitlement + progress together | Good | Self-hostable later if you want full control; EU/US region choice | Good — it's just REST, works from Quest/WebGL too | Easiest — it's Postgres, exportable anytime, can self-host |
| Hybrid (Supabase Auth + itch.io OAuth as *one login option*, not identity source of truth) | Good | Strong | Low-Medium | Free | Best of both — itch email autofill for verification, durable Atomix ID for everything else | Good | Good | Good | Good |

**Decision: Supabase Auth as the source of truth, with an optional "Continue with itch.io" button that uses itch.io OAuth (`profile:me` scope only — the one scope reliably granted) purely to prefill the buyer's itch.io email into the verify-purchase form.** Don't gate anything on the itch.io login itself.

Login flow: email/password or magic-link email, handled by `supabase-csharp` (or raw REST + `UnityWebRequest`, avoiding heavy SDK dependencies). JWT stored in `persistentDataPath` (short-lived access token + refresh token), refreshed silently on launch when online; if offline, the last-known session + cached entitlement is used read-only.

---

## 5. Payment & Entitlement Architecture

```
Atomix User (Supabase user_id) ──┐
                                  ├── entitlements table: { user_id, product="premium_4_8", unlocked_at, source="itch:download_key:XXXX" }
itch.io purchase (download key) ─┘
```

**Exactly how $4.99 becomes a permanent verified unlock:**

1. User clicks **UNLOCK PREMIUM** in-game → browser opens to the `Atomix — Premium Unlock` itch.io page → user pays $4.99 via itch.io/Stripe/PayPal.
2. itch.io shows/emails a **download key** for that purchase.
3. User returns to Atomix, opens **"I've Purchased — Verify Now"**, enters their email + the download key (or logs in with itch.io via OAuth `profile:me` to autofill the email).
4. Client calls your Supabase Edge Function `verify-purchase` (authenticated with the user's Supabase JWT, so you know *which Atomix account* is asking) with `{email, download_key}`.
5. `verify-purchase` (server-side only) calls `GET https://itch.io/api/1/{SECRET_KEY}/game/{PREMIUM_GAME_ID}/download_keys?download_key=...` (or falls back to `?email=...`). Your secret itch.io API key lives only in Supabase's encrypted function secrets — it never reaches the client.
6. If valid and not already claimed by a *different* Atomix account: **write `entitlements` row, `unlocked_at = now()`, permanent, no expiry.** (A download key can be recorded once; reasonable to allow re-verification by the same account, block silent reuse across two different Atomix accounts to deter key-sharing — see §9.)
7. Edge Function returns a **signed entitlement token** (e.g., HMAC-SHA256 or Ed25519 signed JSON: `{user_id, product, unlocked:true, issued_at}`, signed with a server-only private key). The Unity client verifies the signature using an embedded **public** key — it can check authenticity but cannot forge new tokens.
8. Client caches this signed token to `Application.persistentDataPath/entitlement.dat` and sets in-memory `premiumUnlocked = true`. Experiments 4–8 unlock **within seconds**, no restart needed.

**Where entitlement data lives:** authoritative copy in Supabase Postgres (`entitlements` table, protected by Row-Level Security so a user can only ever read their own row). Local cache is a *signed, verifiable copy* for offline use — never the source of truth.

**Preventing simple client-side manipulation:** the local file is a signature-checked token, not a flag. Editing the JSON payload without the private key invalidates the signature; the client detects this and refuses to grant premium (see §9 for how far this actually protects you — it stops casual editing, not a determined reverse engineer).

**Local cache acceptable? Yes**, specifically because it's signed. An unsigned cache (`PlayerPrefs.SetInt`) is explicitly rejected per your requirement.

**Sync across devices / after reinstall:** on any fresh login, the client asks the Edge Function `get-entitlement` (or just re-downloads the signed token) for the logged-in `user_id` — since entitlement lives in Postgres keyed by account, not by machine, logging into the same Atomix account on a second PC, or after a reinstall, or after clearing `PlayerPrefs`/deleting local files, all resolve identically: **log in → server returns the existing entitlement → re-cached locally.** No re-purchase, no re-entering the download key.

**Offline behavior:** if the signed local token exists and is still cryptographically valid, premium stays unlocked with zero network calls. If a user has *never* verified on this machine and has no network, they cannot unlock premium offline for the first time (expected and unavoidable — same as any DRM-light purchase system) but Experiments 1–3 always work.

**Server unavailable:** existing signed tokens keep working (offline-valid by design). *New* verifications simply queue/fail with a clear "try again when online" message — never blocks free content.

---

## 6. Unity Architecture

Build on top of the existing ~111-script project; do not introduce a second save system or duplicate the coin economy. Everything below is a **new, additive layer**; nothing in the existing experiment/coin/Convai scripts needs rewriting, only *gating calls added at their entry points*.

| Component | Responsibility | Persists across scenes | Needs network | Key security note |
|---|---|---|---|---|
| **`AuthManager`** | Login/signup/session refresh against Supabase Auth; holds current `user_id` + JWT | Yes (singleton, `DontDestroyOnLoad`) | Yes (optional at launch — offline mode falls back to last cached session) | Never stores password; only short-lived tokens on disk |
| **`EntitlementManager`** *(this is your existing plan's `LicenseManager.cs`, expanded)* | Requests/caches/validates the signed entitlement token; exposes `bool IsPremiumUnlocked` | Yes | Only to refresh; works from cache offline | Verifies signature with embedded public key; ignores any tampered local file |
| **`PurchaseManager`** | Opens the itch.io purchase URL; drives the "Verify Purchase" UI flow; calls `verify-purchase` Edge Function | No (only active during purchase UI) | Yes (verification requires network) | Sends email/download key over HTTPS only; never stores the itch.io secret key (that never leaves the server) |
| **`ExperimentCatalog`** (ScriptableObject) | Central list of all `ExperimentDefinition`s (id, title, tier, scene ref) — see §7 | N/A (asset, not a MonoBehaviour) | No | Data only |
| **`ExperimentAccessManager`** | Given a catalog + `EntitlementManager`, decides per-experiment locked/unlocked; renders the lock badge/paywall prompt | Yes | No (reads cached state) | Pure gating logic — the only place that decides "can this scene load" |
| **`ProgressManager` / `SaveManager`** | Wraps existing coin economy (`AtomixCoinBank.cs`) + experiment history; local file is authoritative offline, pushed to Postgres `progress` table when online | Yes | Optional | Reuse existing local persistence; add a thin sync layer, don't replace it |
| **`NetworkManager`/`BackendClient`** | Thin `UnityWebRequest` wrapper for all Supabase REST/Edge Function calls, with timeout + retry + offline detection | Yes | Yes | Central point to enforce HTTPS, certificate validation, and never log secrets |
| **`PremiumGate` (UI component)** | Drop-in component on any locked-experiment button; shows the "Premium Experiment / $4.99 / UNLOCK" panel from your mockup | No (per-UI) | No | Pure presentation, delegates decisions to `ExperimentAccessManager` |

**Integration point:** wherever the existing experiment-selection menu currently instantiates/loads Experiment 4–8, insert one call: `if (ExperimentAccessManager.CanAccess(experimentId)) LoadExperiment(...) else PremiumGate.Show(experimentId);`. No other existing script needs to know entitlement exists.

**Convai:** unrelated system, keep as-is; optionally treat "full Convai AI assistant" as a premium perk exactly like your original plan's Strategy 1 suggested — same `ExperimentAccessManager`-style check, reusing the same entitlement flag, no separate system needed.

---

## 7. Experiment Gating Architecture

**Recommendation: ScriptableObject catalog, not enums/hardcoded scenes.**

```csharp
// conceptual shape only — not final code
enum ExperimentTier { Free, Premium }

[CreateAssetMenu] class ExperimentDefinition : ScriptableObject {
    string experimentId;      // stable, never reused
    string displayName;
    ExperimentTier tier;
    SceneReference scene;     // or prefab/addressable reference
    Sprite icon;
}

[CreateAssetMenu] class ExperimentCatalog : ScriptableObject {
    List<ExperimentDefinition> experiments;
}
```

Rules: Experiments 1–3 = `Free`, 4–8 = `Premium`. Adding Experiment 9 later is "create one asset, add to the list" — zero code changes to the monetization system, and zero risk of a future experiment accidentally shipping unlocked because of a missed `if` somewhere. This directly satisfies your "easy to add future experiments" requirement.

---

## 8. Offline / Online Behavior (exact matrix)

| Condition | Free Experiments 1–3 | Premium Experiments 4–8 | Convai | Account/Login |
|---|---|---|---|---|
| First launch, online | Full access | Locked, purchase flow available | Full | Prompt create account / login |
| First launch, **no internet** | Full access | Locked; "Connect to internet to unlock or verify a purchase" | Offline knowledge base fallback only | Can play as guest (local-only progress) or wait to log in |
| Returning user, online, already entitled | Full | Full (server re-confirms silently) | Full | Auto-login via refresh token |
| Returning user, **offline**, already entitled (signed token cached) | Full | **Full** — signed token is valid without network | Offline fallback | Uses cached session |
| Purchased, but verifying right now with no internet | Full | Locked until back online — cannot verify offline (unavoidable, disclosed to user) | — | — |
| Server (Supabase) temporarily down | Full | Stays unlocked if already cached; new verification attempts show "service unavailable, try later," never blocks existing entitlement | Convai itself independently online/offline | Login may fail gracefully with retry, cached session still works |

---

## 9. Security Model — Threat Model

Framed honestly: **this is a $4.99 educational product, not DRM for a AAA title.** The goal is "meaningfully difficult to cheat casually," not "impossible." No client-side Unity game can be made uncrackable — that's true universally, not a flaw specific to this design.

| Threat | Severity | Mitigation |
|---|---|---|
| Edits `PlayerPrefs` directly | Low | Entitlement is never stored in `PlayerPrefs`; it's a signed token file the client validates cryptographically |
| Modifies the local save/entitlement file | Low-Medium | Signature check (public-key verification) rejects any tampered payload; falls back to "not entitled" |
| Patches the `.exe` / IL2CPP binary to force `IsPremiumUnlocked = true` | High (but expected/unavoidable) | No mitigation makes this impossible for a determined attacker with reverse-engineering skill; accept this as the ceiling of client-side protection for an indie title. Optional stronger step (Phase-2, not MVP): keep a small amount of premium-experiment logic/data server-fetched rather than fully bundled, raising the bar without a full rebuild |
| Bypasses UI locks (e.g. loads the locked scene directly via console/mod) | Medium | Re-check entitlement inside the experiment scene's own `Start()`, not only at the menu — defense in depth, cheap to add |
| Disconnects from internet to dodge verification | Low | Doesn't help the attacker — offline mode grants nothing new; premium still requires a previously-valid signed token |
| Fakes/replays an API response to the client | Medium | Entitlement token is signed server-side; a fake unsigned response is rejected. Use HTTPS (TLS) so responses can't be tampered with in transit either |
| Copies another (paying) user's local save/entitlement file to their own machine | Medium | Bind the signed token to the specific `user_id` (embedded in the signed payload) — it only grants premium while logged in as *that* account; doesn't help someone who isn't also logged into the victim's Atomix account (which would require their password) |
| Decompiles the Unity build (asset/IL2CPP extraction) | Medium (accepted risk) | Standard for all Unity indie games; not solvable at this budget/scale. Doesn't expose your itch.io secret key or your users' data, because those never ship in the client |
| Manipulates system clock/timestamps | Low | Entitlement has no expiry to bypass (it's permanent) — a fake clock gains nothing |
| Creates multiple free accounts to get repeated "trial" access | Low | Nothing paid is trial-limited (1–3 are simply free forever) — no incentive to multi-account |
| Attempts to call premium-gated code paths directly (e.g. via a save-editor or debug menu) | Medium | Re-validate entitlement at the point of use (scene load), not only at the menu screen |
| Shares one legitimate download key across many people/accounts | Medium | Server records which `user_id` first claimed a given download key; block/flag a second different account claiming the same key (soft enforcement — you decide whether to hard-block or just log for abuse review) |

---

## 10. Privacy & Legal Considerations

*(Not legal advice — flagged for your own review where noted.)*

- **Data collected:** email + password hash (via Supabase Auth, which uses bcrypt/GoTrue — never touches your own code), a Supabase `user_id`, entitlement status, and progress/coin data. No payment card data ever touches Atomix or Supabase — itch.io/Stripe/PayPal handle that entirely.
- **Minimum-collection principle:** don't collect names, addresses, or ages unless you decide to add a "for schools" tier later. Anonymous/guest play for the free tier (no login required to try Experiments 1–3) is a reasonable option worth considering to reduce data collection and friction.
- **Minors/students:** since this is educational software plausibly used by minors, **⚠️ needs actual legal review** for COPPA (US) / GDPR-K / FERPA-adjacent obligations if used in classrooms — especially if you ever collect anything beyond an email. Consider whether email/password accounts are even necessary for a student audience, vs. a "class code" style anonymous entitlement (this is a product decision worth revisiting, not solved here).
- **Convai:** voice/text sent to Convai's cloud is a third-party data flow outside your control — **⚠️ needs a privacy notice disclosing this**, and its own ToS/DPA review if minors are a target audience.
- **Privacy notice / consent:** ⚠️ you should have a short in-app or itch.io-page privacy notice covering: what's collected (email, purchase status, progress), that Convai sends voice/text to a third party, and that itch.io/Stripe/PayPal handle payment — **have this reviewed if targeting institutions.**
- **Data retention:** keep it simple — retain account + entitlement data as long as the account exists; support a "delete my account" path (Supabase makes this a straightforward `DELETE` respecting RLS) since deletion requests are a common baseline expectation even without formal compliance obligations.
- **Analytics:** none required for MVP; if added later, make it opt-in and disclose it.

---

## 11. Implementation Phases

| Phase | Objective | Key files/components | Dependencies | Definition of done |
|---|---|---|---|---|
| **0 — Architecture decisions** | Lock this document's decisions (Supabase, two itch.io projects, signed-token entitlement) | This document | None | You've approved this plan |
| **1 — Backend setup** | Create Supabase project, schema (`users` via Auth, `entitlements`, `progress`), register itch.io OAuth app (`profile:me`), create the two itch.io projects | Supabase dashboard, itch.io dashboard | Phase 0 | Tables exist; itch.io Premium Unlock product live at $4.99; you have your itch.io secret API key stored only in Supabase function secrets |
| **2 — Auth in Unity** | `AuthManager`: signup/login/session refresh via Supabase REST | New: `AuthManager.cs`, `BackendClient.cs` | Phase 1 | Can create an account and log back in after restart |
| **3 — Entitlement system** | `verify-purchase` + `get-entitlement` Edge Functions; `EntitlementManager.cs` with signature verification | New: 2 Edge Functions, `EntitlementManager.cs`, keypair generated once | Phase 2 | Pasting a real download key flips `IsPremiumUnlocked` to true and it survives restart |
| **4 — Experiment gating** | `ExperimentCatalog` + `ExperimentDefinition` assets for all 8 experiments; `ExperimentAccessManager`; `PremiumGate` UI | New: catalog assets, `ExperimentAccessManager.cs`, `PremiumGate.cs`; edit existing experiment-selection menu to route through it | Phase 3 | Experiments 1–3 always open; 4–8 show the paywall until entitled |
| **5 — Purchase flow UI** | "Premium Experiment / $4.99 / UNLOCK" panel, "Verify Purchase" form, itch.io OAuth-login-for-autofill (optional) | New: `PurchaseManager.cs`, UI prefabs | Phase 4 | Full click-through: locked panel → browser → paste key → unlocked |
| **6 — Persistence/offline** | Local signed-token cache in `persistentDataPath`; offline detection; progress sync layer over existing `AtomixCoinBank.cs` | Edit: `SaveManager`/coin bank integration | Phase 3 | Airplane-mode test: previously-entitled user still gets premium |
| **7 — Security hardening** | Re-check entitlement inside each premium scene's `Start()`; strip/rotate the old committed Convai key per the existing checklist; confirm no secrets in build | Existing experiment scenes (small edit each) | Phase 4–6 | `strings.exe` / build inspection finds no API keys |
| **8 — Testing** | Run the full matrix in §12 | — | Phase 1–7 | All rows pass |
| **9 — Windows build** | Final IL2CPP/Mono build, x86_64, versioned | Build pipeline | Phase 8 | Clean build launches and passes smoke test |
| **10 — itch.io publication** | `butler` push to both itch.io projects, store page copy/screenshots, pricing confirmed at $4.99 | itch.io dashboard, `butler` CLI | Phase 9 | Both projects live; test-purchase end-to-end works publicly |

---

## 12. Testing Matrix

| # | Scenario | Expected result | Pass/fail criteria |
|---|---|---|---|
| 1 | New free user, first launch | Sees account prompt or guest option; 1–3 playable | 1–3 load with no login required (if guest allowed) or after signup |
| 2 | Free user plays 1–3 | Full functionality, no paywall interruptions | No premium prompts appear on free experiments |
| 3 | Free user opens Experiment 4 | Paywall panel with correct $4.99 copy | Panel shows, scene does not load |
| 4 | Purchase success | itch.io checkout completes, key issued | Verify screen accepts the real key |
| 5 | Purchase cancelled mid-checkout | Returns to game with nothing changed | Still locked, no error state stuck on screen |
| 6 | Purchase failed (declined card) | itch.io shows its own failure page | Game shows "not verified yet, try again" on return |
| 7 | Premium unlock after verification | 4–8 unlock | `IsPremiumUnlocked==true` within seconds, no restart needed |
| 8 | Restart app after purchase | Stays unlocked | Cached signed token re-validates on launch |
| 9 | Logout → login same account | Stays unlocked | Server round trip re-confirms entitlement |
| 10 | Reinstall app | Stays unlocked after login | Fresh install + login restores entitlement from server |
| 11 | Login on a second PC | Stays unlocked | Same account, same entitlement, no re-purchase |
| 12 | Offline startup, previously entitled | Full offline access to 4–8 | Airplane mode test passes |
| 13 | Temporary network failure mid-session | No interruption to already-unlocked content | No forced re-check blocks gameplay |
| 14 | Supabase/API down during verification attempt | Graceful "try later" message | No crash, no false-unlock, no false-lock of existing entitlement |
| 15 | Modified `PlayerPrefs` (manually set fake premium key) | No effect | Entitlement unaffected since it's not stored there |
| 16 | Modified/corrupted local entitlement file | Falls back to locked | Signature check fails → treated as not entitled, no crash |
| 17 | Multiple accounts, only one entitled | Only the entitled account unlocks | Switching accounts correctly switches lock state |
| 18 | Corrupted local save data (progress) | Save system recovers/reset gracefully | No crash on launch |
| 19 | Expired/invalid auth session | Prompts re-login | No silent failure; cached entitlement still usable offline if present |
| 20 | Convai unavailable | Offline knowledge base fallback per existing design | No effect on entitlement system |
| 21 | First launch with no internet at all | Free content playable, account creation deferred | No hard block on app usability |
| 22 | Existing premium user launches an updated build | Stays unlocked | Entitlement check unaffected by app version bump |

---

## 13. Cost Analysis

| Service | Purpose | $0 initially | Scales with users? |
|---|---|---|---|
| itch.io | Hosting, payment processing | Yes — free forever, you set revenue share (0% possible) | No per-user fee beyond your chosen % + Stripe/PayPal's own transaction fee (~2.9%+30¢, itch.io absorbs the integration) |
| Supabase (Auth + Postgres + Edge Functions) | Accounts, entitlement, progress | Yes — free tier: 50k MAU, 500MB DB, 500k Edge Function calls/mo | Yes, beyond free tier (~$25/mo Pro plan) once you exceed those limits — unlikely at small/early scale |
| Domain | Not required | Yes — use the `*.supabase.co` and `*.itch.io` URLs, no custom domain needed | N/A |
| Email (magic link/password reset) | Handled by Supabase Auth's built-in mailer | Yes, low volume | Supabase's free-tier email sending has modest caps; fine at small scale |
| Analytics | None required | Yes ($0, none used) | N/A |
| Convai | Existing AI assistant | Depends on your existing Convai plan (unrelated to this system) | Per your existing Convai pricing tier |
| Monitoring | None required for MVP | Yes | Add Supabase's built-in dashboard logs, free |
| CI/build (`butler`, GitHub Actions) | Free itch.io CLI + free GitHub Actions minutes for a small project | Yes | Free tier is generous for a solo project |

**Bottom line: $0 to launch and to run at small scale.** The only genuine future cost is Supabase's Pro tier if you outgrow the free tier's user/storage limits, which is a "good problem to have" scaling cost, not a hidden fixed cost.

---

## 14. Future Expansion

- **Meta Quest / WebGL / Steam:** the entitlement/account layer is pure HTTPS REST — it works identically from any Unity build target. Only the *purchase-verification-and-storefront* half is itch.io-specific; add a Steamworks or Meta-equivalent verification path later as an additional `PurchaseManager` strategy, while `EntitlementManager`/`ExperimentAccessManager`/account system stay untouched.
- **Institutional/school licensing:** the `entitlements` table already models "a product unlocked for a user_id" — a "classroom seat" is just another product row, no architecture change, just a new admin flow to grant it in bulk (e.g. a Supabase Edge Function that takes a CSV of student emails).
- **Additional experiments/premium content:** add to `ExperimentCatalog`, no monetization-system change (§7).
- **Achievements / cloud saves / teacher dashboards:** the `progress` table and Supabase's dashboard/SQL access are a ready foundation — a teacher dashboard is just a read-only view over that table.

---

## 15. File/Code Change Plan (against the existing project)

| Action | What |
|---|---|
| **New files** | `AuthManager.cs`, `BackendClient.cs`, `EntitlementManager.cs`, `PurchaseManager.cs`, `ExperimentAccessManager.cs`, `PremiumGate.cs`, `ExperimentDefinition.cs` + 8 `.asset` instances, `ExperimentCatalog.cs` + 1 `.asset`, 2 Supabase Edge Functions (`verify-purchase`, `get-entitlement`) |
| **Modified** | Experiment-selection menu (route through `ExperimentAccessManager`), each of Experiments 4–8's scene entry point (re-check entitlement in `Start()`), `AtomixCoinBank.cs`/save system (thin sync hook added, logic untouched) |
| **Untouched** | Convai integration internals, existing experiment simulation logic, existing coin-earning gameplay, `implementation_plan.md`'s build/deploy mechanics (Plan 2 stays as-is) |
| **Security cleanup (reuse existing checklist item)** | Rotate/remove the Convai API key currently committed in `Assets/Resources/LabAssistantSettings.asset` before any public build — this was already flagged in `implementation_plan.md` and is unrelated to but should ship alongside this work |

---

## 16. Risks and Open Decisions (need your input before Phase 1)

1. **Guest play vs. mandatory account for free experiments** — recommend allowing guest play of 1–3 with no login, to keep the "strongest free showcase" frictionless; only require login when purchasing/verifying premium. Confirm you're OK with this.
2. **itch.io OAuth scope reality** — you must personally register the OAuth app and test whether itch.io grants anything beyond `profile:me` for your app; the architecture doesn't depend on it, but confirm you're fine with the manual "paste your download key" flow as the guaranteed path (it's a minor UX step, not a blocker).
3. **Refund/chargeback handling** — decide whether a refunded purchase should revoke entitlement (requires periodically re-checking `download_keys` against itch.io, or just accepting the small-scale risk and not building revocation for MVP).
4. **Key-sharing enforcement strictness** — decide whether to hard-block a second account claiming an already-claimed download key, or just log it for manual review (§9).
5. **Legal review items flagged in §10** — minors/education compliance and the Convai data-sharing notice need real legal review, not just this document.

---

## 17. Final Architecture Decision

**Ship the existing single Windows build (all 8 experiments, gated in-app) through two itch.io projects — a free "Atomix" download and a $4.99 "Atomix Premium Unlock" purchase record — with Supabase (Auth + Postgres + Edge Functions) as your own account and entitlement backend. The client never trusts itself: entitlement is granted by a server-side check of the itch.io download key against itch.io's serverside API (using a secret key that never leaves your backend), returned to the client as a cryptographically signed, permanently-cached token that keeps working fully offline once issued, and reappears automatically on any reinstall or new machine the moment the same Atomix account logs back in.**

---

# Appendix: Complete Execution Guide (Free Deployment, Step-by-Step)

This is the literal step list to hand to Claude next, or follow yourself, once you approve the plan above. Every service used has a $0 tier sufficient for this.

## Step A — itch.io setup (free)
1. Create your itch.io account (if not already) at itch.io.
2. New Project → **"Atomix"** → Kind: Downloadable → Pricing: Free (or "$0 or donate"). Leave it in draft until the build is ready.
3. New Project → **"Atomix — Premium Unlock"** → Kind: Downloadable → Pricing: fixed $4.99 → upload a placeholder file (e.g. a short "Thanks — here's how to activate" PDF/TXT). Leave in draft.
4. Go to your account's **API keys** page → generate a personal API key with access to `game:view:purchases` for these projects. **Copy it somewhere safe — this goes into Supabase secrets only, never into the game.**
5. (Optional, for the itch.io-login autofill convenience) Register an OAuth application at itch.io's developer/OAuth settings, request scope `profile:me`, set redirect URI to a loopback address (e.g. `http://127.0.0.1:7890/callback`).

## Step B — Supabase setup (free)
1. Create a free project at supabase.com.
2. In the SQL editor, create:
   - `entitlements (user_id uuid references auth.users, product text, unlocked_at timestamptz, source text, primary key (user_id, product))`
   - `progress (user_id uuid references auth.users primary key, coins int, history jsonb, updated_at timestamptz)`
   - Enable Row-Level Security on both; policy: a user can only `select`/`update` their own row (`auth.uid() = user_id`).
3. In Project Settings → Edge Functions → Secrets, add `ITCHIO_API_KEY` (from Step A.4) and a generated `ENTITLEMENT_SIGNING_KEY` (private key for signing tokens — generate once, keep secret).
4. Write and deploy two Edge Functions (`supabase functions deploy`):
   - `verify-purchase`: takes `{email, download_key}`, calls itch.io's `download_keys` endpoint, on success upserts `entitlements` and returns a signed token.
   - `get-entitlement`: takes the caller's JWT, returns the current signed token if an entitlement row exists.
5. Note your project URL and anon public key — these two (non-secret) values go into the Unity build's config.

## Step C — Unity implementation
Follow §11 Phases 1–7 above. Use `UnityWebRequest` against Supabase's REST/Edge Function endpoints — no heavyweight SDK required, keeping the build small and dependency-light.

## Step D — Build & test
1. Build Windows x86_64 per `implementation_plan.md` Plan 2 (no changes needed there).
2. Run the full matrix in §12 locally, including a real $4.99 test purchase (itch.io supports this normally — you can also fully refund/void a test purchase from your own dashboard afterward).

## Step E — Publish (free)
1. Install `butler` (itch.io's free official CLI): `butler login`, then `butler push <build-folder> youraccount/atomix:windows` and `butler push <placeholder-file> youraccount/atomix-premium-unlock:default`.
2. Flip both itch.io projects from draft to public once verified end-to-end.
3. Rotate the committed Convai API key (existing checklist item) before making the page public.

Total ongoing cost at launch: **$0.** The only cost that ever appears is Supabase's paid tier, and only if you outgrow its free-tier limits — a scaling cost tied to real usage, not a fixed bill.
