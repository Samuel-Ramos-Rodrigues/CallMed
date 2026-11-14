const CACHE_NAME = 'callmed-static-v89-journey';
const OFFLINE_URL = '/offline.html';

// Recursos atuais da interface. As páginas autenticadas não são persistidas.
const STATIC_ASSETS = [
    OFFLINE_URL,
    '/fonts/Manrope.ttf',
    '/js/conta/organizacao.js',
    '/contingencia.html',
    '/css/modules/00-foundations.css',
    '/css/modules/10-navigation.css',
    '/css/modules/20-attendance.css',
    '/css/modules/30-scheduling.css',
    '/css/modules/40-public-and-components.css',
    '/css/modules/50-senai-workflows.css',
    '/css/modules/55-identity-public.css',
    '/css/modules/60-doctor-ui.css',
    '/css/modules/65-dashboards.css',
    '/css/modules/70-clinical-theme.css',
    '/css/modules/80-patient-and-operations.css',
    '/css/modules/90-internal-modules.css',
    '/css/modules/95-booking-calendar.css',
    '/css/modules/99-consultation-journey.css',
    '/js/core/contingencia.js',
    '/js/atendimento/solicitacao-medico-filter.js',
    '/js/core/preferences.js',
    '/js/core/feedback.js',
    '/js/core/shell.js',
    '/js/auth/login.js',
    '/js/core/theme.js',
    '/js/core/accessibility.js',
    '/js/core/mobile-final.js',
    '/js/core/pwa.js',
    '/js/core/date-select.js',
    '/js/auth/identity-register.js',
    '/js/atendimento/confirmacoes.js',
    '/js/atendimento/assistente-flutuante.js',
    '/js/conta/minha-conta.js',
    '/js/atendimento/atendimento.js',
    '/icons/callmed-ui.svg',
    '/images/logo-callmed-horizontal.png',
    '/images/logo-callmed-mark.png',
    '/images/ilustracao-cuidado-clinico.svg',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
    '/icons/maskable-192.png',
    '/icons/maskable-512.png',
    '/icons/apple-touch-icon.png',
    '/manifest.json'
];

const DYNAMIC_PREFIXES = [
    '/Psicologia', '/preparacao', '/Jornada', '/consulta-acao', '/acesso', '/Acessos', '/aplicativo',
    '/Identity/', '/Agente', '/api/', '/Consulta', '/Paciente', '/Funcionario',
    '/FuncionarioPainel', '/Medico', '/MedicoAcesso', '/MedicoPainel', '/Especialidade',
    '/Disponibilidade', '/Disponibilidades', '/MinhaConta', '/Atendimento', '/Agenda',
    '/ListaEspera', '/Confirmacoes', '/Configuracoes', '/Integracoes', '/Solicitacoes',
    '/Convenios', '/Auditoria', '/Relatorios', '/HistoricoExames'
];

const isDynamicPath = pathname => DYNAMIC_PREFIXES.some(prefix =>
    pathname.toLowerCase().startsWith(prefix.toLowerCase()));

async function putIfOk(cache, request, response) {
    if (response && response.ok && !/(?:no-store|private)/i.test(response.headers.get('Cache-Control') || '')) await cache.put(request, response.clone());
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
                const fallback = url.pathname === '/contingencia.html' ? '/contingencia.html' : OFFLINE_URL;
                return (await caches.match(fallback)) || Response.error();
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

    // Somente diretórios públicos conhecidos entram no cache de imagens.
    // Novas rotas e endpoints nunca viram assets por ausência numa lista negativa.
    if (!url.pathname.startsWith('/images/') && !url.pathname.startsWith('/icons/')) return;

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
