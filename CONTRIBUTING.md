# Contributing to KamiYomu

Thank you for considering contributing to KamiYomu! To maintain a high-quality codebase and a smooth development workflow, please follow these guidelines.

## 🚀 Getting Started

We recommend using **Visual Studio 2022** (or later) with the **.NET 8 SDK** installed. However, KamiYomu is fully compatible with **VS Code** for a more lightweight experience.

### Quick Start Workflow
1. **Fork** the repository.
2. **Checkout** the develop branch: `git checkout develop`.
3. **Create** your feature branch: `git checkout -b feature/AmazingFeature`.
4. **Commit** your changes: `git commit -m 'Add some AmazingFeature'`.
5. **Push** to the branch: `git push origin feature/AmazingFeature`.
6. **Open a Pull Request** against the `develop` branch.

---

## 🔄 Keeping Your Branch Updated

Because KamiYomu is under active development, the `develop` branch moves quickly. To avoid merge conflicts:

* **Update Frequently:** Frequently pull changes from the upstream `develop` branch into your local feature branch.
* **Sync Before PR:** Before opening your Pull Request, ensure your branch is rebased or merged with the latest `develop` commits.

---

## 🛠️ Main Development Environments

Choose the environment that best fits your workflow:

| IDE / Editor | Best For... | Documentation |
| :--- | :--- | :--- |
| **Visual Studio** | Full .NET features, integrated debugging, and heavy refactoring. | [View Guide](https://kamiyomu.com/docs/development/visual-studio/) |
| **VS Code** | Cross-platform, lightweight editing, and extension flexibility. | [View Guide](https://kamiyomu.com/docs/development/visual-studio-code/) |

> [!NOTE]
> Regardless of your choice, ensure you have the latest .NET SDK installed as specified in our [Getting Started](https://kamiyomu.com/docs/development/development/) guide.

---

## 🌿 Branching Strategy

We follow a structured branching model to ensure stability. Please target your pull requests correctly.



| Branch | Purpose |
| :--- | :--- |
| `main` | **Production-ready.** Contains the most stable code. |
| `develop` | **Active development.** This is where most work happens. All PRs should target this branch. |
| `releases/*` | **Version tracking.** Contains current and past production versions (e.g., `releases/1.0.0`). |
| `feature/*` | **Feature branches.** Used for developing new features. Named as `feature/your-feature-name`. |
| `hotfix/*` | **Critical Patches.** Used for urgent bug fixes on production. Released immediately upon validation. |

### Release Lifecycle
Before a version reaches the `main` branch, it moves through several stability stages:

1. **Beta (`-beta1`, `-beta2`):** Used for initial testing; may contain inconsistencies or bugs.
2. **Release Candidate (`-rc1`):** Feature-complete and stable. If no major issues are found, the RC becomes the official release.
3. **Production:** The final stable version merged into `main`.

**Example Path:** `releases/1.0.0-beta1` → `releases/1.0.0-rc1` → `releases/1.0.0` (Production)


## 🤝 Join the KamiYomu Community

KamiYomu is built by people who care about great tooling for managing manga. Get involved — your feedback, bug reports, and contributions help shape the project.

| Action | Link |
| :--- | :--- |
| **Discord** | [![Join the discord](https://img.shields.io/discord/1468597233032101942)](https://discord.gg/b9zwEEejsJ) |
| **Discuss** | [![Join the community](https://img.shields.io/github/discussions/kamiyomu/kamiyomu?logo=github&label=Discussions)](https://github.com/KamiYomu/KamiYomu/discussions) |
| **Report** | [![GitHub issues](https://img.shields.io/github/issues/kamiyomu/kamiyomu?logo=github&label=Issues)](https://github.com/kamiyomu/kamiyomu/issues) |
| **Contribute** | [![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?logo=github)](https://github.com/KamiYomu/KamiYomu/pulls) |
