# KamiYomu — A self-hosted, extensible manga reader and download tool

![KamiYomu Owl Logo](./Inkscape/logo-watermark.svg)

**KamiYomu** is a high-performance, extensible manga manager designed for enthusiasts who demand total control. 

By leveraging a modular **Crawler Agent** architecture, KamiYomu empowers you to discover, download, reading, and archive manga from any supported source into a private, self-hosted library.

> [!NOTE]
> **Total Extensibility:** If a site isn't supported yet, you can build your own Crawler Agent in C# and integrate it instantly.

### 🚀 Core Capabilities
* **Modular Crawling:** Support for any website via community-driven **Crawler Agents**.
* **Local Archival:** Download and store high-quality images in a structured local library.
* **Private Hosting:** A built-in web reader to access your collection from any device, anywhere.
* **Developer Friendly:** Comprehensive SDK and Validator tools for building custom agents.
[📖 Read the docs](https://kamiyomu.github.io)

---

[![GitHub followers](https://img.shields.io/github/followers/kamiyomu)](https://github.com/orgs/KamiYomu/followers)
[![GitHub stars](https://img.shields.io/github/stars/kamiyomu/kamiyomu)](https://github.com/kamiyomu/kamiyomu/stargazers)
[![GitHub contributors](https://img.shields.io/github/contributors/kamiyomu/kamiyomu)](https://github.com/kamiyomu/kamiyomu/graphs/contributors)
[![GitHub issues](https://img.shields.io/github/issues/kamiyomu/kamiyomu)](https://github.com/kamiyomu/kamiyomu/issues)
[![GitHub License](https://img.shields.io/github/license/kamiyomu/kamiyomu)](https://github.com/kamiyomu/kamiyomu/blob/main/LICENSE)

---

## 🤝 Join the KamiYomu Community

KamiYomu is built by people who care about great tooling for managing manga. Get involved — your feedback, bug reports, and contributions help shape the project.

| Action | Link |
| :--- | :--- |
| **Discord** | [![Join the discord](https://img.shields.io/discord/1468597233032101942)](https://discord.gg/b9zwEEejsJ) |
| **Discuss** | [![Join the community](https://img.shields.io/github/discussions/kamiyomu/kamiyomu?logo=github&label=Discussions)](https://github.com/KamiYomu/KamiYomu/discussions) |
| **Report** | [![GitHub issues](https://img.shields.io/github/issues/kamiyomu/kamiyomu?logo=github&label=Issues)](https://github.com/kamiyomu/kamiyomu/issues) |
| **Contribute** | [![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?logo=github)](https://github.com/KamiYomu/KamiYomu/pulls) |

---

## 🚀 Why KamiYomu?

Whether you're cataloging rare series, powering a personal manga dashboard, or seeking a cleaner alternative to bloated online readers, KamiYomu puts you in control. It's a lightweight, developer-friendly crawler built for clarity, extensibility, and respectful use of publicly accessible sources.

<img src="./screenshots/welcome-page.jpeg" alt="Welcome Page" width="600"/>

## 📋 Requirements

- [Docker](https://www.docker.com/get-started)

## 📦 Getting Started

1. Save the following `docker-compose.yml` file in a directory:

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
      - /etc/localtime:/etc/localtime:ro # Sync time with host
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


## 🌐 Public API & OPDS Catalog

KamiYomu provides external access to your collection via a public API and the OPDS (Open Publication Distribution System) standard.

### 📚 OPDS Catalog
To access your library on mobile devices, e-readers, or third-party apps (like Moon+ Reader or KyBook), use the following endpoint:

`http://localhost:8080/public/api/v1/opds`

**Note:** If accessing from a different device, replace `localhost` with your server's local IP address (e.g., `192.168.1.50`).

---

### 🛠 Interactive API Documentation (Swagger)

For developers or users wishing to interact with the system programmatically, a full Swagger UI is available to explore all endpoints:

`http://localhost:8080/public/api/swagger/index.html`

---

### ⚠️ Network Configuration
To access these services from outside the host machine:
* **Firewall:** Ensure that port `8080` (or your custom mapped port) is open in your system's firewall.
* **Remote Access:** If accessing over the internet, ensure you have configured port forwarding or a reverse proxy. 
* **Security:** It is highly recommended to use a VPN or a reverse proxy with SSL (HTTPS) if exposing these endpoints publicly.

> [!NOTE]
> Map volumes to local paths as needed. Check [releases](https://github.com/KamiYomu/KamiYomu/releases) for available versions.


## 🧠 Tech Stack

- .NET 8 Razor Pages
- Hangfire for job scheduling
- LiteDB for lightweight persistence
- HTMX + Bootstrap for dynamic UI
- Plugin-based architecture for extensibility

----

## ⚠️ Capabilities & Limitations

### ✅ What KamiYomu Does

* **Complete Manga Reader:** Features a full suite of tools including advanced filtering, reading history, "Chapters of the Week," and dedicated views for new chapters and fresh manga releases.
* **Browser-Based Crawling:** Operates crawler agents that act like a standard web browser to navigate sites, execute JavaScript, and interact with content naturally.
* **Task Orchestration:** Schedules and manages agent tasks such as searching, listing, and fetching metadata.
* **Image Acquisition:** Downloads images directly from sources identified by the crawler agents.
* **Local Archiving:** Automatically organizes downloaded images into structured archives within your specified directories.
* **Flexible Exporting:** Allows you to export your collection as PDF, CBZ, or ZIP files for offline use.
* **Library Management:** Provides a sleek, self-hosted web interface optimized for both desktop and mobile browsing.

---

### ❌ What KamiYomu Does Not

**Security & Data Integrity**
* **Exploit or Invade:** Never bypasses security measures, exploits vulnerabilities, or circumvents paywalls/access controls.
* **Extract Private Data:** Does not scrape databases, protected information, login credentials, or API keys.
* **Data Collection:** Does not collect, monitor, or store any personal data, metadata, or activity on central servers.

**Content & Distribution**
* **Provide Content:** Does not include, host, or serve any built-in manga content. Users must source their own material.
* **Peer-to-Peer Sharing:** Does not support P2P sharing, inter-instance communication, or commercial redistribution.
* **Content Persistence:** Does not cache or retain content on KamiYomu servers; all data exists strictly on the user's local storage.

**Legal & Liability**
* **Endorsement:** Does not support, condone, or assist in bypassing legal restrictions or copyright infringement.
* **Compliance Guarantee:** Does not ensure compliance with third-party Terms of Service or local laws; this is the sole responsibility of the user.
* **Professional Services:** Does not offer legal advice, official support for third-party sites, or warranties regarding software reliability.

---

### ⚖️ User Responsibility & Disclaimer

**KamiYomu is provided "as-is" for local, personal use only.** By using this software, you acknowledge that you are solely responsible for the content you access and how you manage it. The developers and maintainers of KamiYomu assume **no liability** for:
1. User compliance with copyright laws or licensing agreements.
2. Violations of the Terms of Service of any source websites.
3. Any legal consequences arising from the use or misuse of this tool.

Users are encouraged to use KamiYomu only with content they have the legal right to access.

## 📃 License

The KamiYomu project is licensed under the **AGPL-3.0 (Affero General Public License version 3.0)**. This license allows users to freely use, modify, and distribute the software, provided that any modified versions are also distributed under the same license. 

### Key Points of AGPL-3.0:
- **Freedom to Use**: Users can run the software for any purpose.
- **Freedom to Study and Modify**: Users can access the source code and modify it to suit their needs.
- **Freedom to Distribute Copies**: Users can share the original software with others.
- **Freedom to Distribute Modified Versions**: Users can distribute modified versions of the software, but they must also be licensed under AGPL-3.0, ensuring that the same freedoms are preserved for all users.
---

## 🤝 Contributing

Pull requests are welcome! See the [development guide](https://kamiyomu.github.io/docs/development/development/) to get started cloning the project and running it in Visual Studio or VS Code.

Create your own crawler agents by following the [Crawler Agent development guide](https://kamiyomu.com/docs/crawler-agents/create/).

## 💬 Contact

Questions, feedback, or bug reports? [Open an issue](https://github.com/KamiYomu/issues) or start a [discussion](https://github.com/KamiYomu/KamiYomu/discussions).