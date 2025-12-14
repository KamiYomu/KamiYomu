---
title: Visual Studio Setup
parent: Development
nav_order: 3
---

# Visual Studio Code Setup

## Requirements
- Docker: [Download here](https://www.docker.com/get-started)
- Visual Studio: [Download here](https://visualstudio.microsoft.com/downloads/)
- .NET 8 SDK: [Download here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

1. **Clone the repository**
   ```bash
   git clone https://github.com/KamiYomu/KamiYomu.Web.git
	```
2. Open the solution in Visual Studio in `/src/KamiYomu.Web.sln`
3. Set `docker-compose` project as **startup project** (Right-click on project, select `Set As Startup Project.`).
4. Run it