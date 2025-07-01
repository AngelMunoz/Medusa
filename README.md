# 🐍 Medusa

> **⚠️ Experimental Phase**: This project is currently under active experimentation and will likely be merged into [Perla](https://github.com/AngelMunoz/Perla) eventually.

**Medusa** an attempt to have an F# solution for a JavaScript dependency manager that puts **Import Maps** at the center of modern web development. Generate online and offline-ready import maps courtesy of the [Jspm.io](https://jspm.io) API

## ✨ Features

- 🗺️ **Import Map-Centric**: Generate standard ES Import Maps for seamless module resolution
- ⚡ **Zero Duplication**: Packages are cached once in a central store
- 🌐 **Multiple Providers**: Support for JSPM, jsDelivr, Unpkg, and more
- 🔄 **Full Lifecycle**: Install, update, uninstall, and offline operations

## 📁 How It Works

### Central Store For Disk Space Efficiency

```
%LocalAppData%/medusa/v1/store/
├── jquery@3.7.1/
├── vue@3.5.17/
└── xstate@5.19.4/

./node_modules/
├── .medusa/                    (versioned packages)
│   ├── jquery@3.7.1/ → %LocalAppData%/medusa/v1/store/jquery@3.7.1/
│   ├── vue@3.5.17/ → %LocalAppData%/medusa/v1/store/vue@3.5.17/
│   └── xstate@5.19.4/ → %LocalAppData%/medusa/v1/store/xstate@5.19.4/
├── jquery → .medusa/jquery@3.7.1/
├── vue → .medusa/vue@3.5.17/
└── xstate → .medusa/xstate@5.19.4/
```

### Import Map Transformation

**Online Map** (CDN URLs):

```json
{
  "imports": {
    "jquery": "https://ga.jspm.io/npm:jquery@3.7.1/dist/jquery.js",
    "lit": "https://ga.jspm.io/npm:lit@3.3.0/index.js",
    "vue": "https://ga.jspm.io/npm:vue@3.5.17/dist/vue.runtime.esm-browser.prod.js",
    "xstate": "https://ga.jspm.io/npm:xstate@5.19.4/dist/xstate.cjs.mjs"
  },
  "scopes": {
    "https://ga.jspm.io/": {
      "@lit/reactive-element": "https://ga.jspm.io/npm:@lit/reactive-element@2.1.0/reactive-element.js",
      "lit-element/lit-element.js": "https://ga.jspm.io/npm:lit-element@4.2.0/lit-element.js",
      "lit-html": "https://ga.jspm.io/npm:lit-html@3.3.0/lit-html.js",
      "lit-html/is-server.js": "https://ga.jspm.io/npm:lit-html@3.3.0/is-server.js"
    }
  },
  "integrity": {}
}
```

**Offline Map** (Local paths):

```json
{
  "imports": {
    "jquery": "/node_modules/jquery/dist/jquery.js",
    "lit": "/node_modules/lit/index.js",
    "vue": "/node_modules/vue/dist/vue.runtime.esm-browser.prod.js",
    "xstate": "/node_modules/xstate/dist/xstate.cjs.mjs"
  },
  "scopes": {
    "/node_modules/": {
      "@lit/reactive-element": "/node_modules/.medusa/@lit/reactive-element@2.1.0/reactive-element.js",
      "lit-element/lit-element.js": "/node_modules/.medusa/lit-element@4.2.0/lit-element.js",
      "lit-html": "/node_modules/.medusa/lit-html@3.3.0/lit-html.js",
      "lit-html/is-server.js": "/node_modules/.medusa/lit-html@3.3.0/is-server.js"
    }
  },
  "integrity": {}
}
```

## 🛠️ Core Operations

- **Install**: Add new packages and generate import maps
- **Update**: Update existing packages in import maps
- **Uninstall**: Remove packages from import maps
- **GoOffline**: Convert online import maps to offline-ready versions with local caching

## 🙏 Special Thanks

This project wouldn't exist without the incredible work of these projects:

### [JSPM.io](https://jspm.io)

**The backbone of Medusa** - Their powerful API is the reason this entire project works. JSPM's innovative approach to module resolution and CDN distribution makes modern JavaScript package management possible. We are deeply grateful for their open API that enables tools like Medusa to exist.

### [pnpm](https://pnpm.io)

**The inspiration for efficient storage** - pnpm brought the novel approach of central store management with symbolic linking in the JS ecosystem, proving that package managers don't have to waste disk space. Their innovative architecture directly inspired Medusa's zero-duplication storage strategy.

---

_Made with ❤️ for modern web development_
