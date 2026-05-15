import { RagClient } from './RagClient.js';

/**
 * RagUI.js
 * Professional Research Dashboard Orchestrator.
 * Handles DOM management, scientific rendering, and tri-pane interactions.
 */
class RagUI {
    constructor() {
        this.client = new RagClient();
        
        // DOM Elements
        this.chatWindow = document.getElementById('chatWindow');
        this.chatInput = document.getElementById('chatInput');
        this.sendBtn = document.getElementById('sendBtn');
        this.loadingIndicator = document.getElementById('loadingIndicator');
        this.sourceInspector = document.getElementById('sourceInspector');
        this.inspectorContent = document.getElementById('inspectorContent');
        this.toggleInspectorBtn = document.getElementById('toggleInspector');
        this.closeInspectorBtn = document.getElementById('closeInspector');
        this.sessionList = document.getElementById('sessionList');
        
        this.sidebar = document.getElementById('mainSidebar');
        this.toggleSidebarBtn = document.getElementById('toggleSidebar');
        
        this.topKRange = document.getElementById('topKRange');
        this.topKValue = document.getElementById('topKValue');
        this.researchModeToggle = document.getElementById('researchModeToggle');
        this.modelSelector = document.getElementById('modelSelector');
        
        this.mainWorkspace = document.getElementById('mainWorkspace');
        const sid = this.mainWorkspace ? this.mainWorkspace.getAttribute('data-session-id') : null;
        this.currentSessionId = sid && sid !== "" && sid !== "00000000-0000-0000-0000-000000000000" ? sid : null;

        // View State
        this.activeView = 'chat'; // 'chat', 'benchmarks', 'health'
        this.charts = [];

        // Sidebar Persistence: Check before initializing icons
        if (localStorage.getItem('sidebarCollapsed') === 'true') {
            this.toggleSidebar(true, true);
        }

        // Configure Markdown Renderer
        if (window.marked) {
            marked.setOptions({
                highlight: function(code, lang) {
                    if (Prism.languages[lang]) {
                        return Prism.highlight(code, Prism.languages[lang], lang);
                    }
                    return code;
                },
                breaks: true,
                gfm: true
            });
        }

        this.initEventListeners();
        this.loadChatHistory();
        
        this.sourceCache = {};

        // Force initialize icons for static header elements
        if (window.lucide) lucide.createIcons();
    }

    initEventListeners() {
        this.sendBtn.onclick = () => this.handleAsk();
        this.chatInput.onkeydown = (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                this.handleAsk();
            }
            this.autoResizeTextArea();
        };

        if (this.topKRange) {
            this.topKRange.oninput = (e) => {
                this.topKValue.innerText = e.target.value;
            };
        }

        if (this.toggleInspectorBtn) {
            this.toggleInspectorBtn.onclick = () => this.toggleInspector();
        }
        if (this.closeInspectorBtn) {
            this.closeInspectorBtn.onclick = () => this.toggleInspector(false);
        }

        if (this.toggleSidebarBtn) {
            this.toggleSidebarBtn.onclick = () => this.toggleSidebar();
        }

        if (this.sidebarCollapseBtn) {
            this.sidebarCollapseBtn.onclick = () => this.toggleSidebar(true);
        }

        // Keyboard Shortcut: Ctrl+B to toggle sidebar
        document.addEventListener('keydown', (e) => {
            if (e.ctrlKey && e.key === 'b') {
                e.preventDefault();
                this.toggleSidebar();
            }
        });

        const fileInput = document.getElementById('fileInput');
        if (fileInput) {
            fileInput.onchange = () => this.handleUpload(fileInput);
        }

        // Global Click Handler for Dropdowns and View Switching
        document.addEventListener('click', (e) => {
            // Profile Dropdown
            const profileBtn = document.getElementById('profileBtn');
            const profileDropdown = document.getElementById('profileDropdown');
            if (profileBtn && profileBtn.contains(e.target)) {
                e.stopPropagation();
                profileDropdown.classList.toggle('hidden');
                document.getElementById('sidebarSettingsDropdown')?.classList.add('hidden');
            } else if (profileDropdown && !profileDropdown.contains(e.target)) {
                profileDropdown.classList.add('hidden');
            }

            // Settings Dropdown (Updated for Sidebar)
            const settingsBtn = document.getElementById('sidebarSettingsBtn');
            const settingsDropdown = document.getElementById('sidebarSettingsDropdown');
            if (settingsBtn && settingsBtn.contains(e.target)) {
                e.stopPropagation();
                settingsDropdown.classList.toggle('hidden');
                document.getElementById('profileDropdown')?.classList.add('hidden');
            } else if (settingsDropdown && !settingsDropdown.contains(e.target)) {
                settingsDropdown.classList.add('hidden');
            }

            // View Switching (Lab Links) - Support both Sidebar and Header
            const labLink = e.target.closest('.lab-link');
            if (labLink) {
                e.preventDefault();
                const view = labLink.getAttribute('data-view');
                if (view) this.switchView(view);
            }
        });
        
        this.initSidebarLinks();
    }

    initSidebarLinks() {
        // Use event delegation for sidebar to handle dynamic content
        const aside = document.querySelector('aside');
        if (aside) {
            aside.onclick = (e) => {
                // Home/Research Desk link
                const homeLink = e.target.closest('a[href="/"]');
                if (homeLink) {
                    e.preventDefault();
                    this.currentSessionId = null;
                    window.history.pushState({}, '', '/');
                    this.switchView('chat');
                    return;
                }

                // Session Links
                const sessionLink = e.target.closest('a[href*="sessionId="]');
                if (sessionLink && !e.target.closest('button')) {
                    e.preventDefault();
                    try {
                        const url = new URL(sessionLink.getAttribute('href'), window.location.origin);
                        const sid = url.searchParams.get('sessionId');
                        if (sid) this.switchSession(sid);
                    } catch (err) {
                        window.location.href = sessionLink.href;
                    }
                    return;
                }
            };
        }
    }

    async switchView(viewName) {
        const isChat = viewName.toLowerCase() === 'chat';
        this.activeView = viewName.toLowerCase();
        
        // Toggle Chat Input visibility
        const inputContainer = this.chatInput?.closest('.p-8.border-t');
        if (inputContainer) {
            inputContainer.classList.toggle('hidden', !isChat);
        }

        // Deactivate all interactive links
        document.querySelectorAll('aside nav a, header .lab-link').forEach(a => {
            a.classList.remove('bg-zinc-100', 'dark:bg-zinc-900/50', 'border-zinc-200', 'dark:border-zinc-800', 'shadow-sm', 'text-zinc-900', 'dark:text-white', 'bg-zinc-200', 'dark:bg-zinc-800', 'text-indigo-600', 'bg-indigo-500/5', 'dark:text-indigo-400', 'shadow-inner');
        });

        if (isChat) {
            await this.loadChatHistory();
            // Mark home link as active
            const homeLink = document.querySelector('aside a[href="/"]');
            if (homeLink) homeLink.classList.add('text-indigo-600', 'bg-indigo-500/5', 'shadow-inner');
            return;
        }

        this.showLoading(true);
        try {
            const html = await this.client.getLabView(viewName);
            this.chatWindow.innerHTML = `<div class="py-12 animate-in fade-in duration-500">${html}</div>`;
            
            // Mark the link as active
            document.querySelectorAll('.lab-link').forEach(a => {
                const view = a.getAttribute('data-view');
                if (view === viewName) {
                    if (a.closest('header')) {
                        a.classList.add('bg-zinc-200', 'dark:bg-zinc-800', 'text-zinc-900', 'dark:text-white');
                    } else {
                        a.classList.add('text-indigo-600', 'bg-indigo-500/5', 'dark:text-indigo-400');
                    }
                }
            });
            
            if (viewName.toLowerCase() === 'benchmarks') {
                setTimeout(() => this.initCharts(), 100);
            }

            if (window.lucide) lucide.createIcons({ root: this.chatWindow });
        } catch (error) {
            console.error(error);
            this.chatWindow.innerHTML = `<div class="p-12 text-center text-red-500 font-mono text-xs">ERR_VIEW_LOAD_FAILED: ${error.message}</div>`;
        } finally {
            this.showLoading(false);
        }
    }

    initCharts() {
        // Destroy existing charts
        this.charts.forEach(c => c.destroy());
        this.charts = [];

        const ctxLatency = document.getElementById('latencyChart');
        const ctxSimilarity = document.getElementById('similarityChart');

        if (ctxLatency) {
            this.charts.push(new Chart(ctxLatency, {
                type: 'line',
                data: {
                    labels: ['T-5', 'T-4', 'T-3', 'T-2', 'T-1', 'Now'],
                    datasets: [{
                        label: 'Synthesis Latency',
                        data: [420, 380, 510, 440, 490, 460],
                        borderColor: '#6366f1',
                        backgroundColor: 'rgba(99, 102, 241, 0.1)',
                        fill: true,
                        tension: 0.4
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: { y: { beginAtZero: true, grid: { color: 'rgba(0,0,0,0.05)' } }, x: { grid: { display: false } } }
                }
            }));
        }

        if (ctxSimilarity) {
            this.charts.push(new Chart(ctxSimilarity, {
                type: 'doughnut',
                data: {
                    labels: ['High', 'Medium', 'Low'],
                    datasets: [{
                        data: [65, 25, 10],
                        backgroundColor: ['#10b981', '#f59e0b', '#ef4444'],
                        borderWidth: 0
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { position: 'bottom', labels: { boxWidth: 8, font: { size: 10 } } } },
                    cutout: '70%'
                }
            }));
        }
    }

    async switchSession(sessionId) {
        this.activeView = 'chat';
        if (this.currentSessionId === sessionId) return;
        
        this.currentSessionId = sessionId;
        window.history.pushState({}, '', `/?sessionId=${sessionId}`);
        
        // Update UI Active State
        document.querySelectorAll('a[href*="sessionId="]').forEach(l => {
            l.classList.remove('bg-white', 'dark:bg-zinc-900/80', 'border-zinc-200', 'dark:border-zinc-800', 'shadow-sm', 'ring-1', 'ring-black/5', 'dark:ring-white/5');
            try {
                const url = new URL(l.getAttribute('href'), window.location.origin);
                if (url.searchParams.get('sessionId') === sessionId) {
                    l.classList.add('bg-white', 'dark:bg-zinc-900/80', 'border-zinc-200', 'dark:border-zinc-800', 'shadow-sm', 'ring-1', 'ring-black/5', 'dark:ring-white/5');
                    const titleSpan = l.querySelector('span');
                    if (titleSpan) {
                        titleSpan.classList.remove('text-zinc-500', 'group-hover:text-zinc-900', 'dark:group-hover:text-zinc-300');
                        titleSpan.classList.add('text-indigo-600', 'dark:text-white');
                    }
                } else {
                    const titleSpan = l.querySelector('span');
                    if (titleSpan) {
                        titleSpan.classList.add('text-zinc-500');
                        titleSpan.classList.remove('text-indigo-600', 'dark:text-white');
                    }
                }
            } catch (e) {}
        });

        await this.loadChatHistory();
    }

    async loadChatHistory() {
        if (!this.currentSessionId) {
            this.renderLobby();
            return;
        }

        this.chatWindow.innerHTML = '';
        this.showLoading(true);

        try {
            const data = await this.client.getHistory(this.currentSessionId);
            const messages = data.messages || data.Messages || [];
            const selectedModel = data.selectedModel || data.SelectedModel;

            if (selectedModel && this.modelSelector) {
                this.modelSelector.value = selectedModel;
            }

            if (messages.length > 0) {
                messages.forEach(msg => {
                    const role = msg.role || msg.Role;
                    const content = msg.content || msg.Content;
                    const id = msg.id || msg.Id;
                    const sources = msg.sources || msg.Sources;
                    this.appendMessage(role === 'assistant' ? 'ai' : 'user', content, null, id, sources);
                });
            } else {
                this.renderLobby();
            }
        } catch (error) {
            this.renderLobby();
        } finally {
            this.showLoading(false);
        }
    }

    renderLobby() {
        this.chatWindow.innerHTML = `
            <div class="max-w-2xl mx-auto py-32 text-center space-y-10 fade-in">
                <div class="relative inline-block">
                    <div class="absolute -inset-8 bg-indigo-500/10 blur-3xl rounded-full"></div>
                    <div class="relative w-16 h-16 bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-2xl flex items-center justify-center mx-auto shadow-2xl rotate-6">
                        <i data-lucide="sparkles" class="w-8 h-8 text-indigo-500"></i>
                    </div>
                </div>
                <div class="space-y-4">
                    <h1 class="text-4xl font-bold text-zinc-900 dark:text-white tracking-tight">Scientific Synthesis</h1>
                    <p class="text-zinc-400 dark:text-zinc-500 text-sm max-w-sm mx-auto leading-relaxed uppercase tracking-widest font-bold">
                        Phase 01: Context Establishment
                    </p>
                </div>
            </div>
        `;
        if (window.lucide) lucide.createIcons({ root: this.chatWindow });
    }

    async handleAsk() {
        // If not in chat view, switch back first
        if (this.activeView !== 'chat') {
            this.chatWindow.innerHTML = '';
            this.activeView = 'chat';
        }

        const question = this.chatInput.value.trim();
        if (!question) return;

        const topK = this.topKRange ? parseInt(this.topKRange.value) : 3;
        const intent = this.researchModeToggle && this.researchModeToggle.checked ? 1 : 0;
        const selectedModel = this.modelSelector ? this.modelSelector.value : null;

        if (this.chatWindow.querySelector('.py-32')) this.chatWindow.innerHTML = '';

        this.appendMessage('user', question);
        this.chatInput.value = '';
        this.chatInput.style.height = 'auto';
        this.showLoading(true);

        try {
            const data = await this.client.ask(question, this.currentSessionId, topK, intent, selectedModel);
            
            if (!this.currentSessionId && data.sessionId) {
                this.currentSessionId = data.sessionId;
                window.history.pushState({}, '', `/?sessionId=${data.sessionId}`);
                if (this.sessionList) {
                    this.addSessionToSidebar(data.sessionId, data.sessionTitle || question);
                }
            }

            this.appendMessage('ai', data.answer, data.latencyMs, data.messageId, data.sources);
        } catch (error) {
            this.appendMessage('ai', 'Synthesis Error: ' + error.message);
        } finally {
            this.showLoading(false);
        }
    }

    addSessionToSidebar(sessionId, title) {
        if (!this.sessionList) return;
        const container = document.createElement('div');
        container.className = 'group relative flex items-center';
        const link = document.createElement('a');
        link.href = `/?sessionId=${sessionId}`;
        link.className = 'flex-1 flex flex-col gap-1.5 p-4 rounded-2xl border border-transparent hover:border-zinc-200 dark:hover:border-zinc-800 hover:bg-white dark:hover:bg-zinc-900/50 hover:shadow-xl dark:hover:shadow-black/20 transition-all';
        link.innerHTML = `<span class="text-[11px] font-bold text-zinc-500 group-hover:text-zinc-900 dark:group-hover:text-zinc-300 truncate pr-6">${title}</span>`;
        link.onclick = (e) => { e.preventDefault(); this.switchSession(sessionId); };
        container.appendChild(link);
        this.sessionList.insertAdjacentElement('afterbegin', container);
        if (window.lucide) lucide.createIcons({ root: container });
    }

    appendMessage(sender, text, latency, messageId, sources = null) {
        const container = document.createElement('div');
        container.className = `max-w-4xl mx-auto flex gap-8 fade-in group px-4`;
        const avatar = document.createElement('div');
        avatar.className = `w-10 h-10 rounded-xl shrink-0 flex items-center justify-center border transition-all ${sender === 'ai' ? 'bg-zinc-50 dark:bg-zinc-900 border-zinc-200 dark:border-zinc-800 text-zinc-500 dark:text-zinc-400' : 'bg-white border-zinc-200 dark:border-white text-zinc-900 dark:text-black shadow-sm'}`;
        avatar.innerHTML = `<i data-lucide="${sender === 'ai' ? 'bot' : 'user'}" class="w-5 h-5"></i>`;
        const content = document.createElement('div');
        content.className = 'flex-1 min-w-0 space-y-4';
        const textWrapper = document.createElement('div');
        textWrapper.className = `prose prose-sm dark:prose-invert max-w-none text-zinc-700 dark:text-zinc-300 leading-relaxed`;
        if (sender === 'ai') {
            textWrapper.innerHTML = marked.parse(text || "");
            this.renderMath(textWrapper);
        } else {
            textWrapper.innerText = text || "";
        }
        content.appendChild(textWrapper);
        if (sender === 'ai' && sources && sources.length > 0) {
            const sourceGrid = document.createElement('div');
            sourceGrid.className = 'flex flex-wrap gap-2 mt-6';
            sources.forEach((s, idx) => {
                const sourceId = `src-${messageId}-${idx}`;
                this.sourceCache[sourceId] = s;
                const badge = document.createElement('button');
                badge.className = 'px-3 py-1.5 rounded-lg bg-indigo-50/50 dark:bg-indigo-900/20 border border-indigo-100 dark:border-indigo-800/50 text-[10px] font-bold text-indigo-600 dark:text-indigo-400 hover:bg-indigo-100 dark:hover:bg-indigo-900/40 transition-all flex items-center gap-2 shadow-sm';
                badge.innerHTML = `<i data-lucide="file-text" class="w-3 h-3"></i> Source [${idx + 1}]`;
                badge.onclick = () => this.inspectSource(sourceId);
                sourceGrid.appendChild(badge);
            });
            content.appendChild(sourceGrid);
        }
        container.appendChild(avatar);
        container.appendChild(content);
        this.chatWindow.appendChild(container);
        if (window.lucide) lucide.createIcons({ root: container });
        this.chatWindow.scrollTop = this.chatWindow.scrollHeight;
    }

    renderMath(element) {
        try {
            const html = element.innerHTML;
            const rendered = html
                .replace(/\$\$(.*?)\$\$/g, (m, p1) => katex.renderToString(p1, { displayMode: true }))
                .replace(/\$(.*?)\$/g, (m, p1) => katex.renderToString(p1, { displayMode: false }));
            element.innerHTML = rendered;
        } catch (e) {}
    }

    inspectSource(sourceId) {
        const source = this.sourceCache[sourceId];
        if (!source) return;
        this.toggleInspector(true);
        
        const scorePercent = Math.round((source.score || source.Score || 0) * 100);
        
        this.inspectorContent.innerHTML = `
            <div class="space-y-6 animate-in fade-in slide-in-from-right-4 duration-300">
                <div class="space-y-2">
                    <div class="flex items-center justify-between">
                         <span class="text-[10px] font-bold text-indigo-600 dark:text-indigo-400 uppercase tracking-widest">Verified Source</span>
                         <span class="text-[10px] font-mono text-zinc-400 dark:text-zinc-500">${scorePercent}% Match</span>
                    </div>
                    <h4 class="text-sm font-bold text-zinc-900 dark:text-white leading-tight">${source.sourceDocument || source.SourceDocument}</h4>
                </div>
                
                <div class="relative">
                    <div class="absolute -left-4 top-0 bottom-0 w-0.5 bg-indigo-500/30"></div>
                    <p class="text-[11px] text-zinc-600 dark:text-zinc-400 italic leading-relaxed">"${source.content || source.Content}"</p>
                </div>

                <div class="pt-4 border-t border-zinc-100 dark:border-zinc-900">
                    <div class="flex items-center gap-2 text-[9px] text-zinc-400 dark:text-zinc-500 uppercase font-bold tracking-widest">
                        <i data-lucide="shield-check" class="w-3 h-3"></i>
                        <span>Actionable Provenance Bound</span>
                    </div>
                </div>
            </div>
        `;
        if (window.lucide) lucide.createIcons({ root: this.inspectorContent });
    }

    toggleInspector(show) {
        if (show === undefined) show = this.sourceInspector.classList.contains('hidden');
        this.sourceInspector.classList.toggle('hidden', !show);
    }

    toggleSidebar(forceCollapse, instant = false) {
        if (!this.sidebar) return;
        
        const isCollapsed = forceCollapse !== undefined ? forceCollapse : !this.sidebar.classList.contains('w-0');
        
        if (instant) {
            this.sidebar.classList.add('transition-none');
        }

        if (isCollapsed) {
            this.sidebar.classList.remove('w-[300px]');
            this.sidebar.classList.add('w-0', 'opacity-0', 'pointer-events-none');
            localStorage.setItem('sidebarCollapsed', 'true');
        } else {
            this.sidebar.classList.add('w-[300px]');
            this.sidebar.classList.remove('w-0', 'opacity-0', 'pointer-events-none');
            localStorage.setItem('sidebarCollapsed', 'false');
        }

        // Update Toggle Icons
        if (this.toggleSidebarBtn) {
            const icon = isCollapsed ? 'panel-left-open' : 'panel-left';
            this.toggleSidebarBtn.innerHTML = `<i data-lucide="${icon}" class="w-5 h-5"></i>`;
            if (window.lucide) lucide.createIcons({ root: this.toggleSidebarBtn });
        }

        if (instant) {
            // Force reflow
            this.sidebar.offsetHeight;
            this.sidebar.classList.remove('transition-none');
        }
    }

    showLoading(show) {
        this.loadingIndicator.classList.toggle('hidden', !show);
        this.sendBtn.disabled = show;
    }

    async handleUpload(input) {
        const formData = new FormData();
        for (let i = 0; i < input.files.length; i++) formData.append('files', input.files[i]);
        this.showLoading(true);
        try {
            const response = await this.client.uploadFiles(formData);
            if (response.ok) window.location.reload();
        } catch (error) {} finally { this.showLoading(false); }
    }

    autoResizeTextArea() {
        this.chatInput.style.height = 'auto';
        this.chatInput.style.height = (this.chatInput.scrollHeight) + 'px';
    }

    async handleDeleteSession(sessionId, element) {
        if (!confirm('Are you sure you want to delete this research thread?')) return;
        
        try {
            const success = await this.client.deleteSession(sessionId);
            if (success) {
                element.remove();
                if (this.currentSessionId === sessionId) {
                    this.currentSessionId = null;
                    window.history.pushState({}, '', '/');
                    this.renderLobby();
                }
            } else {
                alert('Failed to delete research thread.');
            }
        } catch (error) {
            console.error(error);
            alert('An error occurred during deletion.');
        }
    }

    async handleDeleteDocument(documentId, element) {
        if (!confirm('Are you sure you want to delete this document from the knowledge registry?')) return;
        
        try {
            const success = await this.client.deleteDocument(documentId);
            if (success) {
                element.remove();
            } else {
                alert('Failed to delete document.');
            }
        } catch (error) {
            console.error(error);
            alert('An error occurred during deletion.');
        }
    }
}

document.addEventListener('DOMContentLoaded', () => { window.ui = new RagUI(); });
