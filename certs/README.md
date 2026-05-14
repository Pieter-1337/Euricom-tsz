# Local HTTPS certs (mkcert)

The Vite dev server (and the .NET API) need to serve HTTPS in development.
Use mkcert to generate a locally-trusted cert and drop the files in the
repo-root `certs/` folder.

## 1. Install mkcert

**Windows (choco):**

```powershell
choco install mkcert
```

**Windows (scoop):**

```powershell
scoop bucket add extras
scoop install mkcert
```

**macOS:**

```bash
brew install mkcert
brew install nss   # only needed for Firefox
```

**Linux:**

```bash
sudo apt install libnss3-tools
curl -Lo /tmp/mkcert https://github.com/FiloSottile/mkcert/releases/latest/download/mkcert-v1.4.4-linux-amd64
chmod +x /tmp/mkcert
sudo mv /tmp/mkcert /usr/local/bin/mkcert
```

## 2. Install the local CA

```bash
mkcert -install
```

This adds mkcert's root CA to the OS (and browser) trust stores so any cert
it signs is trusted without warnings.

Fully quit and reopen any already-running browsers afterwards — a tab
reload is not enough; browsers read the trust store on startup.

## 3. Generate the cert

From the repo root:

```bash
mkcert -cert-file certs/local-cert.pem -key-file certs/local-key.pem localhost 127.0.0.1 ::1
```

Name them exactly `local-cert.pem` and `local-key.pem`. Point Vite at them
in `packages/web/vite.config.ts`:

```ts
server: {
  https: {
    key: fs.readFileSync('../../certs/local-key.pem'),
    cert: fs.readFileSync('../../certs/local-cert.pem'),
  },
},
```

## 4. Trust the CA from Node (SSR)

The browser trusts the mkcert root after step 2, but Node uses its own
bundled CA store and will reject the cert during SSR fetches to the API.
Run Node with `--use-system-ca` so it reads from the OS trust store (where
mkcert installed its root). Update the web `dev` script in
`packages/web/package.json`:

```json
"dev": "bun run node --use-system-ca node_modules/vite/bin/vite.js --port 3000"
```

Do not reach for `NODE_EXTRA_CA_CERTS` or undici workarounds —
`--use-system-ca` is the one that works.
