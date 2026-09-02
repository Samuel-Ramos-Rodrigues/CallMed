const CACHE_NAME = 'callmed-static-v21-6-organized';
const OFFLINE_URL = '/offline.html';

// Somente entrypoints e assets realmente usados. Os CSS de versões antigas foram
// consolidados em site.css/public.css/identity.css na V21.3.
const STATIC_ASSETS = [
    OFFLINE_URL,
    '/css/site.css',
    '/css/public.css',
    '/css/identity.css',
    '/js/theme.js',
    '/js/accessibility.js',
    '/js/mobile-final.js',
    '/js/pwa.js',
    '/js/date-select.js',
    '/js/identity-register.js',
    '/js/confirmacoes.js',
    '/js/assistente-flutuante.js',
    '/js/atendimento.js',
    '/icons/callmed-ui.svg',
    '/images/logo-callmed-horizontal.png',
    '/images/logo-callmed-mark.png',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
    '/icons/maskable-192.png',
    '/icons/maskable-512.png',
    '/icons/apple-touch-icon.png',
    '/manifest.json'
];

const DYNAMIC_PREFIXES = [
    '/Identity/', '/Agente', '/api/', '/Consulta', '/Paciente', '/Funcionario',
    '/FuncionarioPainel', '/Medico', '/MedicoAcesso', '/MedicoPainel', '/Especialidade',
    '/Disponibilidade', '/Disponibilidades', '/MinhaConta', '/Atendimento', '/Agenda',
    '/ListaEspera', '/Confirmacoes', '/Configuracoes', '/Integracoes', '/Solicitacoes',
    '/Convenios', '/Auditoria', '/Relatorios'
];

const isDynamicPath = pathname => DYNAMIC_PREFIXES.some(prefix => pathname.startsWith(prefix));

async function putIfOk(cache, request, response) {
    if (response && response.ok) await cache.put(request, response.clone());
    return response;
}

self.addEventListener('install', event => {
    event.waitUntil((async () => {
        const cache = await caches.open(CACHE_NAME);
        // Uma falha isolada de asset não pode impedir a atualização inteira do PWA.
        await Promise.allSettled(STATIC_ASSETS.map(async asset => {
            const request = new Request(asset, { cache: 'reload' });
            const response = await fetch(request);
            if (!response.ok) throw new Error(`${asset}: ${response.status}`);
            await cache.put(request, response);
        }));
        await self.skipWaiting();
    })());
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        const keys = await caches.keys();
        await Promise.all(keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key)));
        await self.clients.claim();
    })());
});

self.addEventListener('message', event => {
    if (event.data?.type === 'SKIP_WAITING') self.skipWaiting();
});

self.addEventListener('fetch', event => {
    const request = event.request;
    if (request.method !== 'GET') return;

    const url = new URL(request.url);
    if (url.origin !== self.location.origin) return;

    // Páginas autenticadas e dados clínicos nunca são persistidos em cache.
    if (request.mode === 'navigate') {
        event.respondWith((async () => {
            try {
                return await fetch(request);
            } catch {
                return (await caches.match(OFFLINE_URL)) || Response.error();
            }
        })());
        return;
    }

    if (isDynamicPath(url.pathname)) return;

    const isInterfaceAsset =
        url.pathname.startsWith('/css/') ||
        url.pathname.startsWith('/js/') ||
        url.pathname === '/manifest.json';

    if (isInterfaceAsset) {
        // Network-first evita o problema mais comum após deploy: PWA abrir com CSS/JS
        // da versão anterior. Offline continua usando o último arquivo válido em cache.
        event.respondWith((async () => {
            const cache = await caches.open(CACHE_NAME);
            try {
                const response = await fetch(request, { cache: 'no-cache' });
                return await putIfOk(cache, request, response);
            } catch {
                return (await cache.match(request)) || (await caches.match(url.pathname)) || Response.error();
            }
        })());
        return;
    }

    // Imagens, ícones e SVG: cache rápido com atualização em segundo plano.
    event.respondWith((async () => {
        const cache = await caches.open(CACHE_NAME);
        const cached = await cache.match(request) || await caches.match(url.pathname);
        const network = fetch(request).then(response => putIfOk(cache, request, response)).catch(() => null);
        return cached || await network || Response.error();
    })());
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const url = new URL(event.notification?.data?.url || '/Confirmacoes?source=pwa', self.location.origin).href;
    event.waitUntil((async () => {
        const windowClients = await clients.matchAll({ type: 'window', includeUncontrolled: true });
        const sameOrigin = windowClients.find(client => client.url.startsWith(self.location.origin));
        if (sameOrigin) {
            if ('navigate' in sameOrigin) await sameOrigin.navigate(url);
            return sameOrigin.focus();
        }
        return clients.openWindow ? clients.openWindow(url) : undefined;
    })());
});
