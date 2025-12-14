# KamiYomu Documentation

The **KamiYomu Documentation** (https://kamiyomu.github.io/docs/) is the official website and resource center for the KamiYomu manga crawler project, providing guides, technical specifications, and usage instructions. Contributions and improvements are highly encouraged via **Pull Requests**.

## How to Contribute

To contribute documentation, fix typos, or update guides, follow these steps to set up your local development environment.

### Local Development Environment Setup

1. Clone the repository, ensuring you include any necessary submodules:

    ```bash
    git clone --recurse-submodules [https://github.com/KamiYomu/KamiYomu.Docs.git](https://github.com/KamiYomu/KamiYomu.Docs.git)
    ```

2. Navigate to the project directory (assuming the repository is named `KamiYomu.Docs`):

    ```bash
    cd KamiYomu.Docs
    ```

3. Build and launch the development environment using Docker to ensure all dependencies are met:

    ```bash
    docker-compose up --build -d
    ```

4. Access the documentation site locally at `http://localhost:4000`. Changes to the Markdown files will automatically reload in your browser.

### Installing Dependencies

If you need to include new dependencies (gems) and update the `Gemfile.lock` file, you can run the following command to install them directly into the Docker volume:

```bash
docker run --rm -v "${PWD}:/usr/src/app" -w /usr/src/app ruby:3.2 bundle install
```

Feel free to contribute and help improve the clarity and completeness of the KamiYomu documentation!

## About the Jekyll Theme

This project utilizes the **[Just the Docs]** theme for Jekyll, which provides a modern, fast, and searchable interface. Key features include support for:

* Deployment on GitHub Pages via Actions
* Easy local builds and previews
* Simplified customization and robust plugin support

## Licensing

This repository is licensed under the [MIT License]. You are welcome to reuse or extend the code and documentation structure. Please let us know how we can improve!

[Just the Docs]: https://just-the-docs.github.io/just-the-docs/
[MIT License]: https://en.wikipedia.org/wiki/MIT_License