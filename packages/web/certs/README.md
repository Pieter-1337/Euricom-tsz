# Local HTTPS certs (mkcert)

The Vite dev server (and the .NET API) serve HTTPS in development using a
mkcert-signed cert. Generate one once per machine and drop the files in the
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

## 3. Generate the cert

From the repo root:

```bash
mkcert -cert-file certs/local-cert.pem -key-file certs/local-key.pem localhost 127.0.0.1 ::1
```

The file names must be exactly `local-cert.pem` and `local-key.pem` — Vite
loads them by path (see `packages/web/vite.config.ts`).

## 4. Trust the CA from Node (SSR)

The browser trusts the mkcert root after step 2, but Node uses its own
bundled CA store and will reject the cert during SSR fetches to the API.
The web dev script already handles this:

```json
"dev": "bun run node --use-system-ca node_modules/vite/bin/vite.js --port 3000"
```

The `--use-system-ca` flag tells Node to read from the OS trust store
(where mkcert installed its root), so SSR `fetch` calls to
`https://localhost:7215` succeed. Do not swap this for
`NODE_EXTRA_CA_CERTS` or undici workarounds — `--use-system-ca` is the
working setup.
