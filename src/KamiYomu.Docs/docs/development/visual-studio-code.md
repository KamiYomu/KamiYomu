---
title: Visual Studio Code Setup
parent: Development
nav_order: 4
---

### Using VsCode

To get started with local development using Visual Studio Code, ensure the following tools are installed:

**Required Tools**

- Docker: [Download here](https://www.docker.com/get-started)
- Visual Studio Code [Download Here](https://code.visualstudio.com/)
- C# Dev Kit Extension [Install](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
- Docker Extension for VS Code [Install](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-docker)

Note: Make sure Docker is installed and running on your machine.

1. Clone the Repository

```bash
    git clone https://github.com/KamiYomu/KamiYomu.Web.git
```

2. Running the Project in VS Code
- Open the `./src/` folder in VS Code.
- Navigate to the "Run and Debug" tab (Ctrl+Shift+D) or press `F5`.
- Select the launch configuration: "Attach to .NET Core in Docker".
- Click the ▶️ Start Debugging button.


This project includes predefined tasks to build and run the Docker container automatically.
If you install all required extensions, the project will run and open the browser in http://localhost:8080
> NOTE:  You may see a window with some error related `=> ERROR [kamiyomu.web internal] load metadata for mcr.microsoft.com/dotnet/sdk:8.0`, just click on `abort` button then try again.