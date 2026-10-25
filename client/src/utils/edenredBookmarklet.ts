const MESSAGE = 'banksync-edenred'
export const ACK = 'banksync-edenred-ack'

export const isEdenredOrigin = (origin: string) => /^https:\/\/([a-z0-9-]+\.)*myedenred\.be$/.test(origin)

export function isEdenredMessage(e: MessageEvent): e is MessageEvent<{ type: string; payload: string }> {
  return isEdenredOrigin(e.origin) && e.data?.type === MESSAGE && typeof e.data.payload === 'string'
}

/** `javascript:` link that runs {@link bookmarklet} on myedenred.be and sends the result to `origin`. */
export const bookmarkletHref = (origin: string) =>
  'javascript:' + encodeURIComponent(`(${bookmarklet.toString()})(${JSON.stringify(origin)},${JSON.stringify(MESSAGE)},${JSON.stringify(ACK)})`)

/**
 * Runs on myedenred.be, stringified, so it must stay self-contained. myEdenred logs in through a
 * browser-only flow, so instead of a server-side login this borrows the headers the myEdenred app
 * itself sends to its API, fetches the operations with them, and posts the JSON to BankSync's
 * /edenred page. The token never leaves the user's browser.
 */
function bookmarklet(origin: string, message: string, ack: string) {
  const w = window as any
  if (!/(^|\.)myedenred\.be$/.test(location.hostname)) {
    alert('Open myedenred.be, log in, then click this bookmark there.')
    return
  }
  if (w.__banksync) return w.__banksync()

  const api = 'https://api.be.edenred.io/xp-user/v1.0/'
  const keep = ['authorization', 'x-client-id', 'x-client-secret', 'accept-language']
  const realFetch = window.fetch
  let headers: Record<string, string> | null = null
  let payload = ''

  const bar = document.createElement('div')
  bar.style.cssText =
    'position:fixed;z-index:2147483647;left:50%;top:12px;transform:translateX(-50%);max-width:90vw;display:flex;gap:8px;' +
    'align-items:center;padding:12px 16px;border-radius:8px;background:#1e293b;color:#fff;font:14px system-ui;box-shadow:0 4px 16px #0006'
  const button = (label: string, onClick: () => void) => {
    const b = document.createElement('button')
    b.textContent = label
    b.style.cssText = 'padding:4px 10px;border:0;border-radius:6px;background:#38bdf8;color:#000;font:inherit;cursor:pointer'
    b.onclick = onClick
    return b
  }
  const close = button('×', () => bar.remove())
  const show = (text: string, ...buttons: HTMLButtonElement[]) => {
    bar.replaceChildren('BankSync: ' + text, ...buttons, close)
    document.body.appendChild(bar)
  }
  w.__banksync = () => document.body.appendChild(bar)

  const copy = button('Copy JSON', () =>
    navigator.clipboard.writeText(payload).then(() => show('copied. Paste it on the BankSync Edenred page.', copy)))

  function send() {
    const target = window.open(origin + '/edenred', 'banksync-edenred')
    if (!target) return show('the popup was blocked. Allow popups, or copy the JSON instead.', sendButton, copy)
    show('sending… finish logging in to BankSync in the new tab if it asks.')
    let tries = 0
    // Repeats until acknowledged: the tab may first pass through the BankSync login.
    const timer = setInterval(() => {
      if (++tries > 180) {
        clearInterval(timer)
        return show('BankSync did not answer. Copy the JSON and paste it on the BankSync Edenred page.', copy)
      }
      target.postMessage({ type: message, payload }, origin)
    }, 1000)
    window.addEventListener('message', function onAck(e) {
      if (e.origin !== origin || e.data?.type !== ack) return
      clearInterval(timer)
      window.removeEventListener('message', onAck)
      show('sent. The BankSync tab shows the result.')
    })
  }
  const sendButton = button('Send to BankSync', send)

  async function load() {
    show('fetching transactions…')
    try {
      const res = await realFetch.call(window, api + 'accounts/accountRef/operations', {
        headers: { ...headers, 'x-correlation-id': crypto.randomUUID(), accept: 'application/json' },
      })
      if (!res.ok) throw new Error('HTTP ' + res.status)
      payload = await res.text()
      show(`${(JSON.parse(payload).data || []).length} transactions ready.`, sendButton, copy)
    } catch (e: any) {
      headers = null // let the next API call of the page retry
      show(`fetching failed (${e.message}). Click around in myEdenred to retry.`)
    }
  }

  function seen(url: unknown, get: (name: string) => string | null | undefined) {
    if (headers || !String(url).startsWith(api)) return
    const found: Record<string, string> = {}
    for (const name of keep) {
      const value = get(name)
      if (value) found[name] = value
    }
    if (!found.authorization) return
    headers = found
    void load()
  }

  const proto = XMLHttpRequest.prototype as any
  const { open, setRequestHeader, send: xhrSend } = proto
  proto.open = function (this: any, ...args: any[]) {
    this.__bsUrl = args[1]
    this.__bsHeaders = {}
    return open.apply(this, args)
  }
  proto.setRequestHeader = function (this: any, name: string, value: string) {
    if (this.__bsHeaders) this.__bsHeaders[name.toLowerCase()] = value
    return setRequestHeader.call(this, name, value)
  }
  proto.send = function (this: any, ...args: any[]) {
    seen(this.__bsUrl, (name) => this.__bsHeaders?.[name])
    return xhrSend.apply(this, args)
  }
  window.fetch = function (input: RequestInfo | URL, init?: RequestInit) {
    try {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url
      const h = new Headers(init?.headers ?? (input instanceof Request ? input.headers : undefined))
      seen(url, (name) => h.get(name))
    } catch {
      // never break the page's own requests
    }
    return realFetch.call(window, input, init)
  }

  show('open your transactions (or any other page) in myEdenred so its login can be picked up.')
}
