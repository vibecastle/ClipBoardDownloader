// Creates the DOM button
function createCBDButton(targetUrl, format = 'normal') {
    const btn = document.createElement("button");
    btn.className = "cbd-inject-btn";
    
    if (format === 'reels') {
        btn.classList.add('cbd-reels-btn');
        btn.innerHTML = `<svg viewBox="0 0 24 24"><path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/></svg><span>CBD</span>`;
    } else {
        btn.innerHTML = `<svg viewBox="0 0 24 24"><path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/></svg> CBD`;
    }
    
    btn.addEventListener('click', (e) => {
        e.preventDefault();
        e.stopPropagation();
        if (btn.classList.contains('cbd-disabled')) return;
        
        let finalUrl = targetUrl;
        if (targetUrl === 'dynamic') {
            finalUrl = window.location.href;
        } else if (targetUrl === 'dynamic-tiktok') {
            finalUrl = findTikTokVideoUrl(btn);
        }
        
        const originalHtml = btn.innerHTML;
        btn.innerHTML = format === 'reels' ? `<span>Wait..</span>` : `Wait...`;
        
        chrome.runtime.sendMessage({ action: "download", url: finalUrl }, (response) => {
            navigator.clipboard.writeText(finalUrl).catch(() => {});
            btn.innerHTML = format === 'reels' ? `<span>OK.</span>` : `OK.`;
            setTimeout(() => btn.innerHTML = originalHtml, 2000);
        });
    });
    
    return btn;
}

function findTikTokVideoUrl(btn) {
    const card = btn.closest('[data-e2e="recommend-list-item-container"], article');
    
    if (card) {
        // 1. Try direct link inside this specific card
        const link = card.querySelector('a[href*="/video/"], a[href*="/photo/"]');
        if (link && link.href) {
            return link.href;
        }

        // 2. Try extracting from player wrapper
        const playerWrapper = card.querySelector('[id^="xgwrapper-"]');
        const authorLink = card.querySelector('a[href^="/@"]');
        
        if (playerWrapper && authorLink) {
            const wrapperId = playerWrapper.getAttribute('id');
            const idParts = wrapperId.split('-');
            const videoId = idParts[idParts.length - 1];
            
            const hrefAttr = authorLink.getAttribute('href');
            const usernameMatch = hrefAttr.match(/@([a-zA-Z0-9_\.]+)/);
            const username = usernameMatch ? `@${usernameMatch[1]}` : null;
            
            if (videoId && username) {
                return `https://www.tiktok.com/${username}/video/${videoId}`;
            }
        }
    }

    return window.location.href;
}

function handleYouTube() {
    if (!window.location.pathname.startsWith('/watch') && !window.location.pathname.startsWith('/shorts/')) return;
    
    const menuContainer = document.querySelector('ytd-menu-renderer #top-level-buttons-computed');
    if (menuContainer && !menuContainer.querySelector('.cbd-inject-btn')) {
        menuContainer.appendChild(createCBDButton('dynamic'));
    }
    
    const shortsContainer = document.querySelector('ytd-shorts-player-controls #actions-inner');
    if (shortsContainer && !shortsContainer.querySelector('.cbd-inject-btn')) {
        shortsContainer.appendChild(createCBDButton('dynamic'));
    }
}

function handleInstagram() {
    if (window.location.pathname.startsWith('/reels/') || window.location.pathname.startsWith('/reel/')) {
        const actionIcons = document.querySelectorAll('svg[aria-label="Like"], svg[aria-label="Curtir"], svg[aria-label="Descurtir"], svg[aria-label="Unlike"]');
        
        actionIcons.forEach(icon => {
            const actionBtnWrapper = icon.closest('div[role="button"], button') || icon.parentElement;
            if (!actionBtnWrapper) return;
            const actionColumn = actionBtnWrapper.parentElement;
            
            if (actionColumn && !actionColumn.dataset.cbdInjected) {
                let postUrl = "dynamic";
                const container = actionColumn.closest('div[style*="width"]') || document.body;
                let link = container.querySelector('a[href*="/reel/"]');
                if (link && !link.getAttribute('href').includes('audio')) {
                    postUrl = "https://www.instagram.com" + link.getAttribute('href');
                }
                
                const btnContainer = document.createElement('div');
                btnContainer.className = 'cbd-ig-reels-container';
                btnContainer.style.display = "flex";
                btnContainer.style.justifyContent = "center";
                btnContainer.appendChild(createCBDButton(postUrl, 'reels'));
                
                actionColumn.insertBefore(btnContainer, actionColumn.firstChild); 
                actionColumn.dataset.cbdInjected = "true";
            }
        });
    }

    const articles = document.querySelectorAll('article:not([data-cbd-injected])');
    articles.forEach(article => {
        const linkElem = article.querySelector('a[href*="/p/"], a[href*="/reel/"]');
        if (!linkElem) return;
        
        const postUrl = "https://www.instagram.com" + linkElem.getAttribute('href');
        const isVideo = article.querySelector('video') !== null || postUrl.includes('/reel/');
        const sections = Array.from(article.querySelectorAll('section'));
        if (sections.length === 0) return;
        
        const actionBar = sections[0];
        let targetContainer = actionBar;
        if (actionBar.firstChild && actionBar.firstChild.tagName !== 'svg') {
            targetContainer = actionBar.firstChild;
            targetContainer.style.display = "flex";
            targetContainer.style.alignItems = "center";
        }
        
        const btnContainer = document.createElement('div');
        btnContainer.className = 'cbd-ig-container';
        
        const btn = createCBDButton(postUrl);
        if (!isVideo) {
            btn.classList.add('cbd-disabled');
            btn.innerHTML = `<svg viewBox="0 0 24 24"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.42 0-8-3.58-8-8 0-1.85.63-3.55 1.69-4.9L16.9 18.31C15.55 19.37 13.85 20 12 20zm6.31-3.1L7.1 5.69C8.45 4.63 10.15 4 12 4c4.42 0 8 3.58 8 8 0 1.85-.63 3.55-1.69 4.9z"/></svg> Not a Video`;
        }
        
        btnContainer.appendChild(btn);
        targetContainer.appendChild(btnContainer);
        article.dataset.cbdInjected = "true";
    });
}

function handleTikTok() {
    // TikTok video pages — inject button near action bar
    const actionContainers = document.querySelectorAll('[data-e2e="like-icon"], [data-e2e="browse-like-icon"]');
    
    actionContainers.forEach(container => {
        const actionColumn = container.closest('[class*="ActionBar"]') || container.parentElement?.parentElement;
        if (!actionColumn || actionColumn.dataset.cbdInjected) return;
        
        const btnContainer = document.createElement('div');
        btnContainer.className = 'cbd-tiktok-container';
        btnContainer.appendChild(createCBDButton('dynamic-tiktok', 'reels'));
        
        actionColumn.appendChild(btnContainer);
        actionColumn.dataset.cbdInjected = "true";
    });

    // Fallback: show floating button on any TikTok video page
    if (window.location.pathname.includes('/video/') || window.location.pathname.includes('/photo/')) {
        injectFloatingButton();
    }
}

function handleGenericSite() {
    injectFloatingButton();
}

function injectFloatingButton() {
    if (document.querySelector('.cbd-floating-btn')) return;
    
    const btn = document.createElement('button');
    btn.className = 'cbd-floating-btn';
    btn.innerHTML = `<svg viewBox="0 0 24 24"><path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/></svg>`;
    btn.title = "Download with CBDownloader";
    
    btn.addEventListener('click', (e) => {
        e.preventDefault();
        e.stopPropagation();
        
        const url = window.location.href;
        const originalHtml = btn.innerHTML;
        btn.innerHTML = `⏳`;
        
        chrome.runtime.sendMessage({ action: "download", url: url }, (response) => {
            navigator.clipboard.writeText(url).catch(() => {});
            btn.innerHTML = `✅`;
            setTimeout(() => btn.innerHTML = originalHtml, 2000);
        });
    });
    
    document.body.appendChild(btn);
}

const hostname = window.location.hostname;

function getSiteHandler() {
    if (hostname.includes('youtube.com')) return handleYouTube;
    if (hostname.includes('instagram.com')) return handleInstagram;
    if (hostname.includes('tiktok.com')) return handleTikTok;
    return handleGenericSite;
}

const handler = getSiteHandler();
let throttleTimer = null;

function scheduleHandler() {
    if (throttleTimer !== null) return;
    throttleTimer = setTimeout(() => {
        throttleTimer = null;
        handler();
    }, 150);
}

const observer = new MutationObserver(scheduleHandler);

observer.observe(document.body, { childList: true, subtree: true });

handler();
