# KamiYomu — Your Self-Hosted Manga Downloader

![KamiYomu Owl Logo](./Inkscape/logo-watermark.svg)

**KamiYomu** is a powerful, extensible manga crawler built for manga enthusiasts who want full control over their collection. It scans and downloads manga from supported websites, stores them locally, and lets you host your own private manga reader—no ads, no subscriptions, no limits.

[📖 Read the docs](https://kamiyomu.github.io)

---

## 💬 Community

Join the conversation and be part of the KamiYomu community:

[![Join the discussion on Reddit](https://badgen.net/badge/reddit/kamiyomu/orange?icon=reddit)](https://www.reddit.com/r/KamiYomu/)
[![Discord](https://flat.badgen.net/badge/Discord/channel/5865F2?icon=discord)](https://discord.com/channels/1451623083604443138)
[![GitHub Discussions](https://flat.badgen.net/badge/GitHub/Discussions/blue?icon=github)](https://github.com/orgs/KamiYomu/discussions)

---

## ✨ Features

- 🔍 **Automated Crawling** — Fetch chapters from supported manga sites with ease
- 💾 **Local Storage** — Keep your manga files on your own server or device
- 🧩 **Plugin Architecture** — Add support for new sources or customize crawling logic
- 🛠️ **Built with .NET 8** — Lightweight, maintainable, and easy to extend

---

## 🚀 Why KamiYomu?

Whether you're cataloging rare series, powering a personal manga dashboard, or seeking a cleaner alternative to bloated online readers, KamiYomu puts you in control. It's a lightweight, developer-friendly crawler built for clarity, extensibility, and respectful use of publicly accessible sources.

<img src="./screenshots/welcome-page.jpeg" alt="Welcome Page" width="600"/>

---

## 📋 Requirements

- [Docker](https://www.docker.com/get-started)

---

## 📦 Getting Started

1. Save the following `docker-compose.yml` file:

```yml
services:
  kamiyomu:
    image: marcoscostadev/kamiyomu:latest
    ports:
      - "8080:8080"
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz"]
      interval: 30s
      timeout: 10s
      retries: 3
    volumes:
      - kamiyomu_manga:/manga
      - kamiyomu_database:/db
      - kamiyomu_agents:/agents
      - kamiyomu_logs:/logs

volumes:
  kamiyomu_manga:
  kamiyomu_database:
  kamiyomu_agents:
  kamiyomu_logs:
```

2. Run the following command in the directory containing `docker-compose.yml`:

```bash
docker-compose up -d
```

3. Access the web interface at `http://localhost:8080`

**Note:** Map volumes to local paths as needed. Check [releases](https://github.com/KamiYomu/releases) for available versions.

---

## 🧠 Tech Stack

- .NET 8 Razor Pages
- Hangfire for job scheduling
- LiteDB for lightweight persistence
- HTMX + Bootstrap for dynamic UI
- Plugin-based architecture for extensibility

---

## 📜 License

This project is licensed under AGPL-3.0. See the [LICENSE](LICENSE) file for details.

---

## 🤝 Contributing

Pull requests are welcome! See the [development guide](https://kamiyomu.github.io/docs/development/) to get started cloning the project and running it in Visual Studio or VS Code.

---

## 💬 Contact

Questions, feedback, or bug reports? [Open an issue](https://github.com/KamiYomu/issues) or start a [discussion](https://github.com/orgs/KamiYomu/discussions).