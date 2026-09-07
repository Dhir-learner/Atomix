# <img align="left" src="https://img.icons8.com/?size=50&id=13186&format=png&color=000000"> Experimenting with Chemical Reactions in Virtual Reality

## Demo
https://drive.google.com/file/d/1W_kMqp_5rlrAXLKSB6BcBMAyuCfBuZzr/view?usp=drivesdk

## 📖 About Atomix

**Atomix** is an educational virtual chemistry laboratory built with **Unity 6** for immersive chemistry learning in **VR**, with a **Windows desktop keyboard-and-mouse fallback**.

The project turns chemistry experiments into an interactive 3D learning experience. Students can choose reactions from an in-game laboratory book, handle virtual glassware and chemicals, control the amount and order of substances they use, receive real-time feedback, learn through molecular-reaction videos, review previous attempts, ask an AI lab assistant questions, and study the chemistry behind successful reactions through scientific graphs.

Atomix is designed around the idea that students should not only see the correct result, but also be able to **experiment, make mistakes, understand why they failed, and try again**.

---

## ✨ Key Features

### 🧪 1. Interactive 3D Chemistry Laboratory

The main laboratory provides a hands-on 3D environment containing virtual beakers, test tubes, pipettes, burners, chemicals, and other laboratory equipment.

Students can:

- Select an experiment from the in-game **Reaction Book**
- Pick up and manipulate laboratory objects
- Pour liquids and add solid substances
- Operate laboratory equipment such as the Bunsen burner
- Observe reaction-specific visual, audio, and textual feedback
- Complete experiments in either VR or desktop mode

The project currently contains **8 chemistry experiments**.

### 🎯 2. Free-Hand Experimentation

The original fixed-sequence reactions were extended into a **free-hand experiment system**.

Instead of simply triggering a reaction by performing a fixed sequence, students must use appropriate quantities and procedures. The shared `FreeHandReactionEngine` tracks live amounts and evaluates the experiment using configurable chemistry rules.

The engine supports:

- Target quantities for each reagent
- Live quantity measurement
- Configurable tolerance ranges
- Overdose detection
- Underdose detection after the mixture settles
- Wrong-order detection
- Discrete solid additions
- Heating-duration validation
- Chemistry-specific failure explanations

All **8 reactions** now support quantity-aware free-hand experimentation.

### ✅ 3. Realistic Success and Failure

Experiments are not simply pass/fail buttons.

Atomix can identify realistic mistakes such as:

- Adding too much reagent
- Adding too little reagent
- Using the wrong order of addition
- Heating for too little or too long
- Performing a required preparation step at the wrong time

Each reaction provides a floating measurement tooltip and a live laboratory measurement tracker. Failure messages explain the chemistry behind the mistake and direct the student toward the AI Lab Assistant for further help.

### 📚 4. Reaction Book and Learning Mode

A world-space **Reaction Book** lets students browse and select experiments.

The book organizes experiments around reaction categories such as:

- Decomposition
- Displacement
- Neutralization
- Redox
- Precipitation
- Combustion

Students can learn before performing through the molecular-level learning system, which uses Unity `VideoPlayer` and a reaction video catalog.

### 🎥 5. Molecular-Level Reaction Learning

Atomix includes a **Learn / Perform** workflow.

Before performing an experiment, the student can access a molecular explanation of the reaction. The project contains a catalog-driven video system using `ReactionLearningController` and `ReactionLearningVideoCatalog`.

The existing project analysis confirms **6 assigned reaction videos**, with the original implementation leaving 2 reaction videos unassigned.

### 🤖 6. In-Lab AI Chemistry Assistant

The AI assistant is now integrated directly into the laboratory instead of being limited to a separate tutor scene.

The current implementation uses the **Convai REST API** and provides:

- Push-to-talk voice interaction
- Spoken AI responses
- Text responses in a right-edge assistant panel
- Current experiment context
- Exact measured quantities and target values
- Chemistry-specific failure explanations
- Graph-specific explanations
- Experiment-attempt context

#### Controls

| Key | Action |
|---|---|
| **V** (hold) | Talk to the AI assistant |
| **M** | Minimise / expand the assistant panel |

The assistant runs in both `LabScene` and `LabAssistantScene`.

In `LabScene`, the assistant is intentionally **voice + panel only** so it does not obstruct the main laboratory. In `LabAssistantScene`, the system can additionally display the assistant character.

> **Important:** Convai credentials are intentionally not committed. `Assets/Resources/LabAssistantSettings.asset` ships without the API key and character ID.

### 🧠 7. Context-Aware AI Assistance

The AI is connected to the experiment state through `ExperimentContextProvider`.

When the student asks for help, the assistant can be given context such as:

- Which reaction is currently being performed
- Which quantities have already been added
- Target quantities and accepted ranges
- Whether the experiment succeeded or failed
- The chemistry-specific reason for failure
- How many previous attempts were made
- How many previous attempts succeeded

This allows the assistant to answer questions about **the student's actual experiment**, rather than only giving generic chemistry information.

### 🗂️ 8. Experiment History

Atomix now records detailed experiment attempts using `ExperimentHistoryManager`.

Each attempt can store:

- Unique attempt ID
- Reaction ID and name
- Timestamp
- Experiment duration
- Final outcome
- Quantities used
- Target quantities
- Step-by-step actions
- Whether each step was correct
- AI questions and responses

History is persisted as JSON in:

```text
Application.persistentDataPath/atomix_experiment_history.json
```

The system stores up to **300 attempts**, removing the oldest entries when the limit is reached.

### 📊 9. Experiment History UI

The history interface is available from inside the laboratory.

Features include:

- **Tab** to open and close history
- Attempts displayed newest first
- Reaction filtering
- Expandable attempt details
- Quantity vs. target comparison
- Full step history
- AI interaction history
- Colour-coded success, failure, and abandoned attempts
- Clear History option

History is designed to help students identify repeated mistakes and understand their own progress.

### 📈 10. Scientific Reaction Graphs

After a successful experiment, Atomix can present a scientific analysis of the reaction through three graph views:

#### Energy Profile

Shows:

- Reactant energy level
- Product energy level
- Reaction-energy curve
- Activation energy (`Ea`)
- Enthalpy change (`ΔH`)
- Transition state

#### Exothermic / Endothermic Analysis

Shows:

- Reactant and product enthalpy levels
- Direction of heat transfer
- Exothermic or endothermic classification
- Explanation of what the student would physically observe

#### Entropy Analysis

Shows:

- Signed entropy change (`ΔS`)
- Zero reference axis
- All 8 reactions on a common comparison scale
- Gibbs free-energy reasoning for the selected reaction

Graphs are available through the `ReactionGraphUI`, and the graph data is stored in a `ReactionGraphCatalog` with one scientific data entry for each of the 8 reactions.

### 🧮 11. Chemistry-Aware Scientific Data

The graph system includes values for:

- `ΔH` — enthalpy change
- `Ea` — activation energy
- `ΔS` — entropy change
- `ΔG` — Gibbs free energy
- Reaction type
- Energy-profile points

The catalog also provides derived values such as:

- Standard Gibbs free energy
- Gibbs free energy at a selected temperature
- Crossover temperature where meaningful
- Reverse activation energy
- Peak energy and peak position
- Spontaneity summaries

`ΔH` and `ΔS` were derived from standard thermodynamic data for the reaction equations used in the project. Activation-energy values are literature-plausible and exposed as editable fields.

### 🧩 12. AI Explanation of Graphs

Each scientific graph includes an **Ask AI to Explain** action.

The AI receives the graph's scientific values and explanation context, allowing the student to ask about the specific graph being displayed.

The response is:

- Spoken
- Shown in the assistant panel
- Recorded in experiment history

This connects practical experimentation with thermodynamic and kinetic interpretation.

### 🖥️ 13. Desktop Fallback

Atomix can run without a VR headset using keyboard and mouse controls.

The desktop system is injected at runtime by `DesktopBootstrap.cs`, which adapts the laboratory's VR-oriented interaction environment for desktop use.

#### Main Desktop Controls

| Key / Input | Action |
|---|---|
| **W A S D** | Move |
| **Mouse** | Look |
| **Left Shift** | Sprint |
| **Space / Ctrl** | Fly up / down |
| **Left Click** | Grab / release / interact |
| **R** | Release held object |
| **Q / E** | Rotate held object |
| **Z / X / C / V** | Rotate held object on other axes |
| **T** | Reset held-object pose |
| **B** | Toggle reaction book |
| **Arrow Keys** | Flip book pages |
| **1 – 8** | Quick-select reaction |
| **V** | Push-to-talk in the assistant |
| **Tab** | Experiment history |
| **F** | Scientific graphs |
| **Escape** | Toggle cursor lock / close learning UI |

### 🥽 14. VR / XR Support

The main target is **Meta Quest / OpenXR**.

The project uses Unity XR technologies for immersive interaction, including:

- XR-based object grabbing
- World-space interface interaction
- VR locomotion
- Virtual laboratory interaction
- Hand and controller-oriented interaction

The desktop mode is dynamically enabled when VR input is unavailable.

---

## 🧪 Chemical Experiments

Atomix currently contains these 8 experiments:

| # | Experiment | Reaction |
|---|---|---|
| **1** | Sodium + Water | `2Na + 2H₂O → 2NaOH + H₂` |
| **2** | Sulfuric Acid + Copper(II) Oxide | `H₂SO₄ + CuO → CuSO₄ + H₂O` |
| **3** | Hydrochloric Acid + Sodium Bicarbonate | `HCl + NaHCO₃ → NaCl + H₂O + CO₂` |
| **4** | Potassium + Water | `2K + 2H₂O → 2KOH + H₂` |
| **5** | Aluminum + Iodine | `2Al + 3I₂ → 2AlI₃` |
| **6** | Calcium Oxide + Water | `CaO + H₂O → Ca(OH)₂` |
| **7** | Calcium Carbonate Decomposition | `CaCO₃ → CaO + CO₂` |
| **8** | Iron Sulfate Decomposition | `2FeSO₄ → Fe₂O₃ + SO₂ + SO₃` |

---

## 🏗️ Project Architecture

The project is organized around a set of Unity scenes and reusable runtime systems.

### Scenes

```text
MainMenuScene
│
├── LabScene
│   ├── Reaction Book
│   ├── 3D Laboratory
│   ├── Free-Hand Reaction System
│   ├── Experiment History
│   ├── In-Lab AI Assistant
│   └── Scientific Graphs
│
├── TestingPhaseLab
│   └── Randomized theory + practical testing
│
└── LabAssistantScene
    └── Dedicated AI tutor environment
```

### Major Systems

```text
User Input
├── VR Controllers / XR
└── Keyboard + Mouse
        │
        ▼
DesktopBootstrap / XR Interaction
        │
        ▼
Reaction Book
        │
        ├── Learn / Perform
        │       └── ReactionLearningController
        │
        ▼
ControlReactions
        │
        ├── Reaction 1
        ├── Reaction 2
        ├── Reaction 3
        ├── Reaction 4
        ├── Reaction 5
        ├── Reaction 6
        ├── Reaction 7
        └── Reaction 8
                │
                ▼
        FreeHandReactionEngine
                │
        ┌───────┴────────┐
        ▼                ▼
 Experiment History    AI Context
        │                │
        └───────┬────────┘
                ▼
          Convai Assistant
                │
                ▼
          Text + Voice
                │
                ▼
     Scientific Graph Analysis
```

### Important Scripts

| Script | Responsibility |
|---|---|
| `ControlReactions.cs` | Central reaction selection and laboratory setup |
| `FlipPages.cs` | Reaction-book navigation |
| `Reaction.cs` | Sodium + water reaction logic |
| `Reaction_h2so4_cuo.cs` | H₂SO₄ + CuO reaction |
| `Reaction_hcl_nahco3.cs` | HCl + NaHCO₃ reaction |
| `KOHReaction.cs` | Potassium + water reaction |
| `ReactionAli3.cs` | Aluminum + iodine reaction |
| `reactionCaOH.cs` | CaO + H₂O reaction |
| `CaCO3Reaction.cs` | CaCO₃ decomposition |
| `Feso4Reaction.cs` | FeSO₄ decomposition |
| `FreeHandReactionEngine.cs` | Shared quantity/order/failure logic |
| `ExperimentHistoryManager.cs` | Attempt storage and persistence |
| `ExperimentHistoryUI.cs` | History browsing interface |
| `InLabAssistantController.cs` | In-lab AI assistant |
| `ExperimentContextProvider.cs` | Sends experiment context to AI |
| `ReactionGraphData.cs` | Scientific graph data model |
| `ReactionGraphCatalog.cs` | Catalog of graph data for all reactions |
| `ReactionGraphRenderer.cs` | Graph rendering |
| `ReactionGraphUI.cs` | Graph interface and AI explanation actions |

---

## 🛠️ Technology Stack

| Technology | Use |
|---|---|
| **Unity 6000.3.7f1** | Game engine |
| **Unity XR / OpenXR** | VR support |
| **XR Interaction Toolkit** | VR interaction and locomotion |
| **Unity Input System** | Input handling |
| **TextMeshPro** | Text and world-space UI |
| **Convai REST API** | AI assistant |
| **Unity Microphone / AudioSource** | Voice input and playback |
| **Unity VideoPlayer** | Molecular learning videos |
| **ScriptableObject** | Scientific graph data catalog |
| **JSON / persistentDataPath** | Experiment-history persistence |

---

## 🎮 Core User Journey

```text
Launch
  ↓
Main Menu
  ↓
Enter Laboratory
  ↓
Open Reaction Book
  ↓
Choose Reaction
  ↓
Learn or Perform
  ↓
Perform the experiment manually
  ↓
Measure quantities / follow procedure
  ↓
Success or chemistry-specific failure
  ↓
Record attempt in Experiment History
  ↓
Ask AI for help when needed
  ↓
On success → Scientific Graphs
  ↓
Ask AI to explain the graph
  ↓
Try another reaction / review history
```

---

## 🧪 Testing / Assessment Mode

The `TestingPhaseLab` provides a separate assessment environment with randomized tasks.

The existing testing system supports:

- Practical reaction tasks
- Theoretical multiple-choice questions
- Randomized question selection
- Countdown timing
- Practice-only mode
- Theory-only mode
- Combined practice + theory mode

The project analysis records **8 practical tasks + 10 theoretical questions = 18 total task types**.

---

## 🔬 Scientific Learning Through the 8 Reactions

The scientific graph data currently describes the thermodynamic and kinetic characteristics of all 8 reactions.

| # | Type | ΔH (kJ/mol) | Ea (kJ/mol) | ΔS (J/mol·K) |
|---|---|---:|---:|---:|
| 1 | Exothermic | -368.6 | 10 | -15.5 |
| 2 | Exothermic | -63.7 | 48 | -43.0 |
| 3 | Endothermic | +31.4 | 45 | +241.0 |
| 4 | Exothermic | -393.2 | 6 | +44.7 |
| 5 | Exothermic | -627.6 | 85 | -86.9 |
| 6 | Exothermic | -65.2 | 46 | -26.3 |
| 7 | Endothermic | +178.3 | 185 | +160.7 |
| 8 | Endothermic | +340.1 | 365 | +377.4 |

This makes the project useful not only for practical chemistry simulation, but also for teaching:

- Reaction energetics
- Activation energy
- Enthalpy
- Entropy
- Gibbs free energy
- Spontaneity
- Exothermic and endothermic behaviour
- The relationship between practical observations and chemical theory

---

## 📦 Project Structure

```text
Atomix/
└── VR/
    ├── Assets/
    │   ├── AnimatorControllers/
    │   ├── Resources/              (AI + graph + runtime data)
    │   ├── Oculus Hands/
    │   ├── Prefabs/
    │   ├── Resources/
    │   ├── Scenes/
    │   ├── Scripts/
    │   ├── Sounds/
    │   ├── TextMesh Pro/
    │   └── XR/
    ├── Packages/
    └── ProjectSettings/
```

> The current assistant implementation is **Convai-based**. The older Inworld integration was removed from the working tree during Task 3.

---

## 🚀 Getting Started

### Requirements

- **Unity 6000.3.7f1**
- Windows desktop support for desktop builds
- Android Build Support for Meta Quest builds
- OpenXR / XR support for VR
- A compatible Meta Quest headset for VR testing
- Convai credentials for live AI features

### Open the Project

Open the **`VR/`** folder as the Unity project.

Allow Unity to resolve packages and import project assets before building.

### AI Assistant Setup

The current AI integration uses Convai.

Configure:

```text
Assets/Resources/LabAssistantSettings.asset
```

with the required Convai credentials and character information.

The repository intentionally does not ship with a live API key.

### Windows Build

Build the project for:

```text
PC, Mac & Linux Standalone
```

The desktop fallback provides keyboard-and-mouse access to the laboratory.

### Quest Build

For VR:

1. Open Unity Build Settings.
2. Select **Android**.
3. Configure OpenXR / Quest support.
4. Use the appropriate Quest development settings.
5. Build and deploy to the connected headset.

---

## ⚠️ Current Status and Known Limitations

Atomix is a **functional academic prototype**. The completed Tasks 1–4 substantially extend the original system, but some areas still require final Play-mode validation.

### Known limitations

- A complete end-to-end Play-mode regression pass for Tasks 1–4 is still recommended.
- Convai requires valid credentials for live AI interaction.
- Experiment-history persistence is implemented, but full runtime round-trip testing is still recommended.
- Re-selecting an experiment resets the free-hand measurement state, but some previous visual substance state may remain in the scene.
- The current molecular-video system does not yet fully pass the specific video just watched into the AI context for follow-up questions.
- The checked-in `Assembly-CSharp.csproj` is stale and still references removed Inworld files; Unity can regenerate it when the project is opened.
- `LabAssistantScene` may contain inert legacy Inworld objects with missing-script warnings after the provider migration; they do not represent the active assistant implementation.
- The graph system has been rendered and validated offline, but Unity import, panel sizing, and final Play-mode interaction should still be checked.

---

## 📊 Current Feature Status

| Feature | Status |
|---|---|
| Interactive 3D chemistry laboratory | ✅ |
| VR + desktop support | ✅ |
| 8 chemistry experiments | ✅ |
| Free-hand quantity-based experimentation | ✅ |
| Overdose / underdose / order validation | ✅ |
| Chemistry-specific failure feedback | ✅ |
| Reaction Book | ✅ |
| Learn / Perform molecular videos | ✅ |
| Experiment history and persistence | ✅ |
| In-lab AI assistant | ✅ |
| AI experiment-context awareness | ✅ |
| AI voice + panel responses | ✅ |
| Scientific energy / enthalpy / entropy graphs | ✅ |
| AI graph explanations | ✅ |
| Follow-up AI questions tied specifically to the last molecular video | ⚠️ Partial |
| Full final Play-mode regression | ⏳ Recommended |

---

## 🎓 Educational Value

Atomix combines several learning approaches in one environment:

**Learning by Doing**  
Students physically perform chemistry experiments instead of only reading about them.

**Learning from Mistakes**  
Incorrect quantities and procedures create meaningful failure states rather than generic errors.

**Reflection and Progress Tracking**  
Experiment History records what the student did, how much was used, what failed, and what the student asked the assistant.

**AI-Supported Learning**  
Students can ask questions about their current experiment and receive voice and text explanations.

**Visual Scientific Reasoning**  
Scientific graphs connect practical experiments with reaction energetics, entropy, and Gibbs free energy.

**Immersive Learning**  
VR interaction makes laboratory equipment and procedures available in a safe digital environment.

---

## 👥 Target Users

- Chemistry students
- School and early-university learners
- Teachers and laboratory instructors
- Researchers exploring VR-assisted and AI-assisted chemistry education

---

## 📌 Project Summary

**Atomix** is more than a virtual demonstration of chemical reactions. It is an interactive chemistry learning environment where students can:

> **Choose → Learn → Experiment → Make Mistakes → Understand → Ask AI → Review → Analyse → Try Again**

The project's combination of **VR laboratory interaction, free-hand chemistry validation, experiment history, context-aware AI assistance, molecular learning videos, and scientific reaction graphs** creates a single environment that connects practical experimentation with theoretical understanding.

---

## 👨‍💻 Project Information

- **Engine:** Unity 6000.3.7f1
- **Primary Platform:** Meta Quest / OpenXR
- **Secondary Platform:** Windows Desktop
- **AI Provider:** Convai REST API
- **Scenes:** 4
- **Chemistry Experiments:** 8
- **Assessment Tasks:** 8 practical + 10 theoretical
- **Project Type:** Academic / Educational Prototype
