# KamiYomu — Your Self-Hosted Manga Crawler

![KamiYomu Owl Logo](./Inkscape/logo-watermark.svg)

**KamiYomu** is a powerful, extensible manga crawler built for manga enthusiasts who want full control over their collection. It scans and downloads manga from supported websites, stores them locally, and lets you host your own private manga reader—no ads, no subscriptions, no limits.

---

## ✨ Features

- 🔍 **Automated Crawling**  
  Fetch chapters from supported manga sites with ease.

- 💾 **Local Storage**  
  Keep your manga files on your own server or device.

- 🧩 **Plugin Architecture**  
  Add support for new sources or customize crawling logic.

- 🛠️ **Built with .NET Razor Pages**  
  Lightweight, maintainable, and easy to extend.

---

## 🚀 Why KamiYomu?

Whether you're cataloging rare series, powering a personal manga dashboard, or seeking a cleaner alternative to bloated online readers, KamiYomu puts you in control of how you access and organize manga content. It’s a lightweight, developer-friendly crawler built for clarity, extensibility, and respectful use of publicly accessible sources. Content availability and usage rights depend on the licensing terms of each source — KamiYomu simply provides the tools.
 
 <br/>
 <img src="./screenshots/welcome-page.jpeg" alt="Welcome Page" width="600"/>
 <br/>

## Requirements

- [Docker](https://www.docker.com/get-started)


## 📦 Getting Started

save the following `docker-compose.yml` file to run KamiYomu with Docker:

```yml
services:
  kamiyomu:
    image: marcoscostadev/kamiyomu:latest # Check releases for latest versions
    ports:
      - "8080:8080" # HTTP Port
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz"]
      interval: 30s
      timeout: 10s
      retries: 3
    volumes:
      - ./AppData/manga:/manga # Your desired local path for manga storage
```

In the folder where you saved the `docker-compose.yml` file, run:

```bash
    docker-compose up -d
```


You will have access to the web interface at `http://localhost:8080`.
Keep in mind to map the volumes to your desired local paths. 
See the releases branchs for identifying the versions available.

## 🧠 Tech Stack- .NET 8 Razor Pages
- Hangfire for job scheduling
- LiteDB for lightweight persistence
- HTMX + Bootstrap for dynamic UI
- Plugin-based architecture for source extensibility

## 📜 License
This project is licensed under AGPL-3.0. See the LICENSE file for details.

## 🤝 Contributing
Pull requests are welcome! If you have ideas for new features, plugin sources, or UI improvements, feel free to open an issue or submit a PR.

## 💬 Contact
Questions, feedback, or bug reports? Reach out via GitHub Issues or start a discussion.