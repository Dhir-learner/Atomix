# 🚀 Atomix Deployment & Monetization Strategy

## Project Summary

**Atomix** is a **Unity 6** educational VR chemistry laboratory game with a desktop (keyboard+mouse) fallback. It features 8 interactive chemistry experiments, an AI lab assistant (Convai), molecular animations, scientific graphs, a testing/quiz mode with a coin economy, and more.

| Attribute | Value |
|---|---|
| **Engine** | Unity 6000.3.7f1 |
| **Primary Platform** | Meta Quest VR (OpenXR) |
| **Secondary Platform** | Windows Desktop (keyboard+mouse) |
| **Render Pipeline** | Built-in (BiRP) |
| **AI Backend** | Convai REST API (online) + Offline knowledge base |
| **Total Scripts** | ~111 C# files |

---

## 🎯 Deployment Plans (All Free)

Below are **5 different deployment plans** ranked from easiest to most ambitious. Each plan can be used independently or combined.

---

### Plan 1: 🌐 WebGL Build → Itch.io (Easiest & Fastest)

> **Best for:** Reaching the widest audience immediately, no downloads needed

#### How It Works
1. Build Atomix as a **Unity WebGL** target
2. Upload the build folder to **itch.io** (free)
3. Anyone with a browser can play — no install required

#### Step-by-Step
```
1. Open Unity → File → Build Settings → WebGL
2. Player Settings:
   - Compression Format: Gzip (best for itch.io)
   - Memory Size: 512 MB (for the 3D models)
   - Decompression Fallback: Enabled
3. Build → produces a folder with index.html + Build/ + TemplateData/
4. Go to itch.io → Create New Project → Kind: HTML
5. Zip the build folder → Upload
6. Set iframe dimensions: 1920×1080
7. Publish!
```

#### Limitations
- ❌ **VR will NOT work** in WebGL — desktop mode only
- ❌ Convai AI assistant needs CORS-compatible endpoints
- ❌ Large 3D models (55 MB desk mesh) will cause long load times
- ❌ Windows-only `OfflineVoice.cs` (System.Speech) won't work
- ⚠️ `PlayerPrefs` maps to browser `localStorage` (coins/history persist per browser)

#### Modifications Needed
- Disable/guard VR-specific code with `#if !UNITY_WEBGL` preprocessor directives
- Strip or compress the `pupitru1.fbx` (55.6 MB) — critical for web load times
- Replace `System.Speech` calls with WebGL-safe fallbacks
- Add a loading progress bar (WebGL template)

#### Monetization Compatibility: ⭐⭐⭐⭐
- itch.io supports **"Name Your Own Price"** (including $0)
- Can sell premium versions or DLC later
- itch.io takes **0% by default** (you choose the revenue share)
- Can embed **donation buttons**

---

### Plan 2: 🖥️ Windows Desktop Build → Itch.io / GitHub Releases (Easy)

> **Best for:** Full-featured desktop experience with download

#### How It Works
1. Build Atomix for **Windows Standalone**
2. Upload the `.zip` to **itch.io** or **GitHub Releases**
3. Users download and run the `.exe`

#### Step-by-Step
```
1. Open Unity → File → Build Settings → PC, Mac & Linux Standalone
2. Architecture: x86_64
3. Build → produces an .exe + _Data folder
4. Zip everything together
5. Upload to:
   Option A: itch.io → Create Project → Kind: Downloadable → Upload .zip
   Option B: GitHub → Your repo → Releases → Create Release → Attach .zip
```

#### Platforms
| Platform | Cost | Storage Limit | Bandwidth |
|---|---|---|---|
| **itch.io** | Free | Unlimited | Unlimited |
| **GitHub Releases** | Free | 2 GB per file | Unlimited |
| **Google Drive** | Free | 15 GB total | Limited |
| **MEGA** | Free | 20 GB total | Bandwidth caps |

#### Limitations
- ✅ Full features work (desktop mode, AI, offline voice, coins, history)
- ❌ No VR (unless user has a PCVR headset connected)
- ⚠️ Windows Defender may flag unsigned `.exe` — consider code signing later

#### Modifications Needed
- **None for basic deployment!** The desktop build works as-is
- Optional: Add an installer using **Inno Setup** (free) for a professional feel
- Optional: Rotate the Convai API key before publishing

#### Monetization Compatibility: ⭐⭐⭐⭐⭐
- itch.io: Pay-what-you-want, paid, or free
- Can later move to **Steam** (one-time $100 fee) for wider reach
- GitHub: Free distribution, link to a Patreon/Ko-fi for donations

---

### Plan 3: 🥽 Meta Quest (SideQuest) — Free VR Distribution

> **Best for:** Getting the VR version to real Quest users for free

#### How It Works
1. Build Atomix as an **Android APK** for Meta Quest
2. Upload to **SideQuest** (free for developers)
3. Quest users sideload the app

#### Step-by-Step
```
1. Open Unity → File → Build Settings → Android
2. Player Settings:
   - Minimum API Level: Android 10 (API 29)
   - Target API Level: Android 12 (API 32)
   - Scripting Backend: IL2CPP
   - Target Architecture: ARM64
   - Texture Compression: ASTC
3. XR Settings:
   - Enable OpenXR
   - Add Meta Quest Feature Group
   - Set Render Mode: Multi-Pass or Single-Pass Instanced
4. Build → produces .apk file
5. Go to sidequestvr.com → Upload App → Fill details → Upload APK
6. Users install via SideQuest app
```

#### Platforms
| Platform | Cost | Approval | Audience |
|---|---|---|---|
| **SideQuest** | Free | Minimal review | ~2M+ users |
| **Meta App Lab** | Free | Meta review required | Quest Store discovery |
| **Meta Quest Store** | Free to submit | Strict review | Full Quest audience |

#### Limitations
- ⚠️ The 55 MB desk mesh is a **critical problem** for Quest standalone — needs optimization
- ⚠️ Built-in Render Pipeline may have Quest performance issues
- ⚠️ Convai requires internet — Quest must be on WiFi
- ❌ `System.Speech` (OfflineVoice) won't work on Android

#### Modifications Needed
- **Optimize `pupitru1.fbx`**: Decimate from 55 MB to ~5-10 MB using Blender (free)
- Add texture compression and LODs for Quest hardware
- Replace `System.Speech` with Android TTS API or disable offline voice
- Test and fix frame rate — Quest needs sustained 72 FPS (13.9ms budget)
- Consider switching to **URP** for better Quest performance (significant effort)

#### Monetization Compatibility: ⭐⭐⭐⭐
- SideQuest: Free + paid apps supported
- Meta App Lab / Quest Store: Full commerce support (Meta takes 30% cut)
- Can offer a free demo + paid full version

---

### Plan 4: 📱 Progressive Web App (PWA) — Self-Hosted on GitHub Pages / Netlify

> **Best for:** A branded web presence with your own URL

#### How It Works
1. Build a **WebGL** version of Atomix
2. Host it on **GitHub Pages**, **Netlify**, or **Cloudflare Pages** (all free)
3. Wrap it as a PWA for installability

#### Step-by-Step
```
1. Build Unity WebGL (same as Plan 1)
2. Create a GitHub repository
3. Push the WebGL build to the `gh-pages` branch (or `docs/` folder)
4. Enable GitHub Pages in repo Settings → Pages
5. Your game is live at: https://yourusername.github.io/atomix/

Alternative: Netlify
1. Go to app.netlify.com → New Site → Import from Git
2. Set build command: (empty, already built)
3. Set publish directory: your WebGL build folder
4. Deploy!
```

#### Free Hosting Options

| Platform | Free Tier | Custom Domain | Bandwidth |
|---|---|---|---|
| **GitHub Pages** | Unlimited sites | Yes (free) | 100 GB/month |
| **Netlify** | 100 GB bandwidth | Yes (free) | 100 GB/month |
| **Cloudflare Pages** | Unlimited bandwidth | Yes (free) | Unlimited |
| **Vercel** | 100 GB bandwidth | Yes (free) | 100 GB/month |

#### Modifications Needed
- Same WebGL modifications as Plan 1
- Add a `manifest.json` and service worker for PWA functionality
- Add meta tags for SEO and social sharing
- Create a landing page around the game

#### Monetization Compatibility: ⭐⭐⭐
- Can add a **donation button** (Ko-fi, Buy Me a Coffee, PayPal)
- Can gate premium features behind a login (e.g., Firebase Auth + Firestore)
- Harder to directly charge for a web game vs. a downloadable/store game
- Can run **Google AdSense** on the landing page (not in-game)

---

### Plan 5: 🏫 Educational Platform Distribution

> **Best for:** Reaching schools, colleges, and educational institutions

#### How It Works
1. Build for **Windows Desktop** and/or **Quest APK**
2. Distribute through educational channels
3. Build a portfolio/landing page

#### Channels (All Free)
| Channel | How | Audience |
|---|---|---|
| **Google Classroom** | Share download link with teachers | Schools using Google |
| **Microsoft Teams for Education** | Share via Teams channels | Schools using Microsoft |
| **University LMS** (Moodle, Canvas) | Upload as course resource | Your college + others |
| **ResearchGate / Academia.edu** | Publish alongside your research paper | Researchers |
| **Product Hunt** | Launch as a product | Tech-savvy educators |
| **Reddit** (r/Unity3D, r/VirtualReality, r/chemistry) | Post and share | Enthusiasts |
| **LinkedIn** | Share as a project | Professional network |

#### Modifications Needed
- Create a **landing page** (can be a free GitHub Pages site)
- Write clear documentation and setup guides (you already have great docs!)
- Create a **demo video** (you have one on Google Drive)
- Optional: Package as a `.msi` installer for easy school deployment

#### Monetization Compatibility: ⭐⭐⭐⭐⭐
- **B2B / Institutional licensing**: Charge schools/colleges per-seat or per-year
- **Freemium model**: Free for individuals, paid for institutions with analytics/reporting
- **Grant funding**: Apply for educational technology grants (many available worldwide)
- **Consulting**: Offer customization services for specific curricula

---

## 💰 Monetization Strategies

### Strategy 1: Freemium Model (Recommended)

```
FREE TIER                          PAID TIER ($4.99 - $9.99)
├── 3 reactions                    ├── All 8 reactions
├── Basic AI (offline only)        ├── Full Convai AI assistant
├── No testing mode                ├── Testing + quiz mode
├── No graphs                      ├── Scientific graphs
└── No history                     ├── Experiment history
                                   ├── Downloadable reports
                                   └── Achievements system
```

**Implementation:**
- Add a `LicenseManager.cs` script that checks a boolean flag
- Lock reactions 4-8, testing scene, and graph UI behind the flag
- Use itch.io's payment system or integrate a simple license key check

### Strategy 2: In-App Coin Purchases

> You already have a coin economy! This is the easiest path.

| Package | Price | Coins |
|---|---|---|
| Starter Pack | $0.99 | 200 coins |
| Lab Bundle | $2.99 | 750 coins |
| Mega Pack | $4.99 | 1,500 coins |
| Unlimited | $9.99 | Infinite coins |

**Implementation:**
- Your `AtomixCoinBank.cs` already handles persistence
- Add a **purchase flow** using:
  - **itch.io**: Built-in payments
  - **Meta Quest Store**: Oculus Platform SDK (free to integrate)
  - **Steam**: Steamworks SDK
  - **Web**: Stripe / PayPal integration

> [!IMPORTANT]
> Be careful with in-app purchases in educational software for minors — check **COPPA** regulations and be transparent about monetization.

### Strategy 3: Licensing to Schools (B2B)

| License | Price | Includes |
|---|---|---|
| Single Teacher | Free | 1 install, personal use |
| Classroom (30 seats) | $49/year | 30 installs, reporting |
| Department | $149/year | Unlimited installs, LMS integration |
| Institution | $499/year | Campus-wide, custom branding |

**Implementation:**
- Add a license key system
- Build a simple admin dashboard (can be a free Google Sheets + Apps Script)
- Add usage analytics (opt-in) to demonstrate value to institutions

### Strategy 4: Donations & Sponsorship

| Platform | How |
|---|---|
| **Ko-fi** | One-time donations, no fees |
| **Buy Me a Coffee** | Similar to Ko-fi |
| **GitHub Sponsors** | Monthly sponsorship |
| **Patreon** | Tiered membership with perks |
| **Open Collective** | For open-source projects |

### Strategy 5: Ads (Web Version Only)

- Place banner ads around the WebGL game frame (not in-game)
- Use **Google AdSense** on the landing/wrapper page
- Expected revenue: Low (~$1-5/day per 1000 visitors) but fully passive
- Keep the game itself ad-free for a good user experience

---

## 📊 Comparison Matrix

| | Plan 1: WebGL + itch.io | Plan 2: Desktop + itch.io | Plan 3: Quest SideQuest | Plan 4: Self-Hosted PWA | Plan 5: Educational |
|---|---|---|---|---|---|
| **Difficulty** | ⭐⭐ Medium | ⭐ Easy | ⭐⭐⭐ Hard | ⭐⭐ Medium | ⭐ Easy |
| **Reach** | 🌍 Global (browser) | 💻 Windows users | 🥽 Quest owners | 🌍 Global (browser) | 🏫 Schools |
| **VR Support** | ❌ No | ⚠️ PCVR only | ✅ Full VR | ❌ No | Depends on build |
| **AI Assistant** | ⚠️ Limited | ✅ Full | ⚠️ WiFi only | ⚠️ Limited | ✅ Full |
| **Setup Time** | 2-4 hours | 30 minutes | 1-2 days | 2-4 hours | 1-2 hours |
| **Code Changes** | Moderate | None | Significant | Moderate | None |
| **Monetization** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Cost** | $0 | $0 | $0 | $0 | $0 |

---

## ✅ Recommended Approach

> [!TIP]
> **Start with Plan 2 (Windows Desktop → itch.io)** because it requires **zero code changes** and gets your game out there in 30 minutes. Then expand to other platforms.

### Phased Rollout

```
Phase 1 (Day 1):     Desktop Build → itch.io (free download)
Phase 2 (Week 1):    Add a landing page on GitHub Pages
Phase 3 (Week 2-3):  WebGL build → itch.io (browser playable)
Phase 4 (Month 1):   Quest APK → SideQuest (after optimization)
Phase 5 (Month 2+):  Apply freemium model or institutional licensing
```

---

## ⚠️ Pre-Deployment Checklist

Before deploying **any** build publicly:

- [ ] **Rotate the Convai API key** — it's committed in `Assets/Resources/LabAssistantSettings.asset` and in git history
- [ ] **Remove or hide the API key** from the build (use environment variables or a server proxy)
- [ ] **Rename the product** from "UnityLab by AlinaInc" if desired (but plan for PlayerPrefs migration)
- [ ] **Add a privacy notice** — especially if Convai sends voice data to the cloud
- [ ] **Add credits/attributions** for any third-party assets
- [ ] **Test the build** end-to-end in the target platform
- [ ] **Create a compelling itch.io page** with screenshots, the demo video, and a description

---

## Open Questions

> [!IMPORTANT]
> 1. **Which platform do you want to deploy to first?** (I recommend Plan 2: Desktop → itch.io as it's the fastest)
> 2. **Do you want to keep the VR features** in the initial deployment, or is desktop-only fine for now?
> 3. **Are you comfortable rotating/removing the Convai API key** before publishing, or do you want to disable AI features in the public build?
> 4. **Do you want to rename the product** from "UnityLab" to "Atomix" before deploying? (This will reset existing PlayerPrefs data)
> 5. **Which monetization strategy interests you most?** (Freemium, coins, institutional licensing, donations, or ads?)
