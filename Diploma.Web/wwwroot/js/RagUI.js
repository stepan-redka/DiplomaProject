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
        this.cancelBtn = document.getElementById('cancelBtn');
        this.exportBtn = document.getElementById('exportBtn');
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

        // New Session Modal Elements
        this.newSessionModal = document.getElementById('new-session-modal');
        this.newSessionForm = document.getElementById('new-session-form');
        this.newThreadBtn = document.getElementById('new-thread-btn');
        this.closeNewSessionModal = document.getElementById('close-new-session-modal');
        this.cancelNewSessionBtn = document.getElementById('cancel-new-session');
        this.newSessionModalOverlay = document.getElementById('new-session-modal-overlay');


        this.mainWorkspace = document.getElementById('mainWorkspace');
        const sid = this.mainWorkspace ? this.mainWorkspace.getAttribute('data-session-id') : null;
        this.currentSessionId = sid && sid !== "" && sid !== "00000000-0000-0000-0000-000000000000" ? sid : null;

        // View State
        this.activeView = 'chat'; // 'chat', 'benchmarks', 'health'
        this.charts = [];
        this.activeRequestController = null;

        // Sidebar Persistence: Check before initializing icons
        if (localStorage.getItem('sidebarCollapsed') === 'true') {
            this.toggleSidebar(true, true);
        }

        // Configure Markdown Renderer
        if (window.marked) {
            const markedConfig = {
                breaks: true,
                gfm: true
            };

            if (typeof marked.use === 'function') {
                marked.use(markedConfig);
            } else if (typeof marked.setOptions === 'function') {
                marked.setOptions(markedConfig);
            }
        }

        this.initEventListeners();
        this.loadChatHistory();
        this.updateExportLink();

        this.sourceCache = {};

        // Force initialize icons for static header elements
        if (window.lucide) lucide.createIcons();
    }

    initEventListeners() {
        if (this.sendBtn) {
            this.sendBtn.onclick = () => this.handleAsk();
        }
        if (this.cancelBtn) {
            this.cancelBtn.onclick = () => this.cancelActiveRequest();
        }
        if (this.chatInput) {
            this.chatInput.onkeydown = (e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    this.handleAsk();
                }
                this.autoResizeTextArea();
            };
        }

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

        // New Session Modal Listeners
        if (this.newThreadBtn) {
            this.newThreadBtn.onclick = () => this.toggleNewSessionModal(true);
        }
        if (this.closeNewSessionModal) {
            this.closeNewSessionModal.onclick = () => this.toggleNewSessionModal(false);
        }
        if (this.cancelNewSessionBtn) {
            this.cancelNewSessionBtn.onclick = () => this.toggleNewSessionModal(false);
        }
        if (this.newSessionModalOverlay) {
            this.newSessionModalOverlay.onclick = () => this.toggleNewSessionModal(false);
        }
        if (this.newSessionForm) {
            this.newSessionForm.onsubmit = (e) => this.handleCreateSession(e);
        }

        const documentList = document.getElementById('documentList');
        if (documentList) {
            documentList.addEventListener('click', (e) => {
                // If we clicked the trash button, don't trigger binding
                if (e.target.closest('button')) return;

                const docItem = e.target.closest('.document-item');
                if (docItem) {
                    const docId = docItem.getAttribute('data-doc-id');
                    if (docId) {
                        this.handleToggleDocumentBinding(docId, docItem);
                    }
                }
            });
        }

        // Keyboard Shortcut: Ctrl+B to toggle sidebar
        document.addEventListener('keydown', (e) => {
            if (e.ctrlKey && e.key === 'b') {
                e.preventDefault();
                this.toggleSidebar();
            }
             if (e.key === 'Escape') {
                this.toggleNewSessionModal(false);
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

        // RE-ATTACH SIDEBAR BUTTONS GLOBALLY
        window.handleDeleteSession = (sid, el, e) => {
            if (e) e.stopPropagation();
            this.handleDeleteSession(sid, el);
        };
        window.handleDeleteDocument = (did, el, e) => {
            if (e) e.stopPropagation();
            this.handleDeleteDocument(did, el);
        };

        this.initSidebarLinks();
    }

    toggleNewSessionModal(show) {
        if (!this.newSessionModal) return;
        this.newSessionModal.classList.toggle('hidden', !show);
        if (show) {
            document.getElementById('session-title').focus();
        }
    }

    async handleCreateSession(event) {
        event.preventDefault();
        const form = event.target;
        const title = form.elements['title'].value.trim() || 'New Research Thread';
        const selectedDocumentIds = Array.from(form.querySelectorAll('input[name="selectedDocumentIds"]:checked'))
            .map(checkbox => checkbox.value);

        this.toggleNewSessionModal(false);
        this.showLoading(true);

        try {
            const response = await this.client.createSession(title, selectedDocumentIds);
            if (response && response.sessionId) {
                window.location.href = `/?sessionId=${response.sessionId}`;
            } else {
                window.showAlert('Session Error', 'Failed to create session. The server returned an invalid response.', 'error');
                this.showLoading(false);
            }
        } catch (error) {
            window.showAlert('Network Error', 'Failed to communicate with the server while creating the session.', 'error');
            this.showLoading(false);
        }
    }

    async handleToggleDocumentBinding(documentId, element) {
        if (!this.currentSessionId) {
            window.showAlert('Action Required', 'Please select or create a chat session first to bind this document.', 'info');
            return;
        }

        element.style.opacity = '0.5';
        try {
            const response = await this.client.toggleDocumentBinding(this.currentSessionId, documentId);
            element.classList.toggle('active-binding', response.bound);
        } catch (error) {
            window.showAlert('Binding Failed', 'Could not toggle document binding due to a network error.', 'error');
        } finally {
            element.style.opacity = '1';
        }
    }

    updateDocumentBindingsUI(boundDocumentIds = []) {
        const docItems = document.querySelectorAll('.document-item');
        docItems.forEach(item => {
            const docId = item.getAttribute('data-doc-id');
            const isBound = boundDocumentIds.includes(docId);
            item.classList.toggle('active-binding', isBound);
        });
    }

    async handleExport() {
        if (!this.currentSessionId) {
            window.showAlert('Export Findings', 'Active session required for export.', 'info');
            return;
        }

        if (!this.exportBtn) return;

        const originalHtml = this.exportBtn.innerHTML;
        this.exportBtn.disabled = true;
        this.exportBtn.classList.add('opacity-60', 'pointer-events-none');
        this.exportBtn.innerHTML = '<i data-lucide="loader-2" class="w-4 h-4 animate-spin"></i><span>Exporting</span>';
        if (window.lucide) lucide.createIcons({ root: this.exportBtn });

        try {
            const response = await fetch(`/Chat/ExportSession?sessionId=${this.currentSessionId}`);
            if (!response.ok) {
                const message = await response.text();
                window.showAlert('Export Findings', message || 'Unable to export this research thread.', 'info');
                return;
            }

            const blob = await response.blob();
            const disposition = response.headers.get('content-disposition') || '';
            const fileNameMatch = disposition.match(/filename\*?=(?:UTF-8''|")?([^";]+)/i);
            const fileName = fileNameMatch ? decodeURIComponent(fileNameMatch[1].replace(/"/g, '')) : 'Research_Findings.pdf';
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            link.remove();
            URL.revokeObjectURL(url);
        } catch (error) {
            window.showAlert('Export Findings', 'Unable to generate the research export. Please try again.', 'error');
        } finally {
            this.exportBtn.disabled = false;
            this.exportBtn.classList.remove('opacity-60', 'pointer-events-none');
            this.exportBtn.innerHTML = originalHtml;
            if (window.lucide) lucide.createIcons({ root: this.exportBtn });
            this.updateExportLink();
        }
    }

    initSidebarLinks() {
        // Use event delegation for sidebar to handle dynamic content
        const aside = document.querySelector('aside');
        if (aside) {
            aside.onclick = (e) => {
                // Ignore if clicking a button (delete)
                if (e.target.closest('button')) return;

                // Home/Research Desk link
                const homeLink = e.target.closest('a[href="/"]');
                if (homeLink) {
                    e.preventDefault();
                    this.currentSessionId = null;
                    window.history.pushState({}, '', '/');
                    this.switchView('chat');
                    this.updateExportLink();
                    return;
                }

                // Session Links
                const sessionLink = e.target.closest('a[href*="sessionId="]');
                if (sessionLink) {
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
        this.showChatInput(isChat);

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

            // Re-initialize Lucide icons for the new content
            if (window.lucide) {
                lucide.createIcons({ root: this.chatWindow });
            }

            // --- RESTORE SCIENTIFIC PLOTS ---
            if (viewName.toLowerCase() === 'benchmarks') {
                this.initBenchmarksCharts();
            }

        } catch (error) {
            window.showAlert('Navigation Failed', `Could not load ${viewName} view: ${error.message}`, 'error');
            this.chatWindow.innerHTML = `<div class="p-12 text-center text-red-500 font-mono text-xs">ERR_VIEW_LOAD_FAILED: ${error.message}</div>`;
        } finally {
            this.showLoading(false);
        }
    }

    initBenchmarksCharts() {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.initBenchmarksCharts(), { once: true });
            return;
        }

        const container = document.getElementById('benchmarksData');
        if (!container) return;

        const chartGrid = document.getElementById('benchmarksChartGrid');
        const emptyState = document.getElementById('benchmarksEmptyState');
        const fallbackText = 'No empirical telemetry recorded yet. Process queries or ingest documents to populate scientific graphs.';

        const showEmptyState = (show) => {
            if (chartGrid) chartGrid.classList.toggle('hidden', show);
            if (emptyState) emptyState.classList.toggle('hidden', !show);
            if (show && emptyState && window.lucide) lucide.createIcons({ root: emptyState });
        };

        const readJsonArray = (attributeName) => {
            try {
                const raw = container.getAttribute(attributeName);
                const value = raw ? JSON.parse(raw) : [];
                return Array.isArray(value) ? value.filter(item => item && typeof item === 'object') : [];
            } catch (error) {
                console.warn(`Invalid benchmarks payload in ${attributeName}`, error);
                return [];
            }
        };

        const valueOf = (row, ...keys) => {
            for (const key of keys) {
                if (row && row[key] !== undefined && row[key] !== null) return row[key];
            }
            return null;
        };

        const toNumber = (value) => {
            const numeric = Number(value);
            return Number.isFinite(numeric) ? numeric : 0;
        };

        const hasPositiveMetric = (items, ...keys) => {
            return items.some(item => keys.some(key => toNumber(valueOf(item, key)) > 0));
        };

        const setChartFallback = (canvas, show) => {
            const wrapper = canvas?.parentElement;
            if (!wrapper) return;

            wrapper.classList.add('relative');
            canvas.classList.toggle('hidden', show);

            let placeholder = wrapper.querySelector('[data-chart-fallback]');
            if (show) {
                if (!placeholder) {
                    placeholder = document.createElement('div');
                    placeholder.setAttribute('data-chart-fallback', 'true');
                    placeholder.className = 'absolute inset-0 flex items-center justify-center px-4 text-center text-xs font-semibold leading-relaxed text-zinc-500 dark:text-zinc-400';
                    placeholder.textContent = fallbackText;
                    wrapper.appendChild(placeholder);
                }
            } else if (placeholder) {
                placeholder.remove();
            }
        };

        const datasetHasValues = (data) => {
            if (!data || !Array.isArray(data.datasets) || data.datasets.length === 0) return false;

            return data.datasets.some(dataset => {
                if (!dataset || !Array.isArray(dataset.data) || dataset.data.length === 0) return false;

                return dataset.data.some(point => {
                    if (typeof point === 'number') return Number.isFinite(point) && point > 0;
                    if (!point || typeof point !== 'object') return false;

                    return ['x', 'y'].some(key => Number.isFinite(Number(point[key])) && Number(point[key]) > 0);
                });
            });
        };

        try {
            const latencyData = readJsonArray('data-latency');
            const ingestionData = readJsonArray('data-ingestion');
            const retrievalData = readJsonArray('data-retrieval');
            const throughputData = readJsonArray('data-throughput');
            const densityData = readJsonArray('data-density');
            const storageData = readJsonArray('data-storage');
            const chartPayload = {
                latencyData,
                ingestionData,
                retrievalData,
                throughputData,
                densityData,
                storageData
            };

            console.log('Chart Payload:', chartPayload);

            const hasTelemetry =
                hasPositiveMetric(latencyData, 'value', 'Value') ||
                hasPositiveMetric(ingestionData, 'value', 'Value') ||
                hasPositiveMetric(retrievalData, 'value', 'Value') ||
                hasPositiveMetric(throughputData, 'value', 'Value') ||
                hasPositiveMetric(densityData, 'value', 'Value') ||
                hasPositiveMetric(storageData, 'value', 'Value');

            // Dispose old charts before deciding whether to render this view.
            this.charts.forEach(c => c.destroy());
            this.charts = [];

            if (!window.Chart) {
                console.warn('Chart.js is not available; benchmarks charts cannot be rendered.');
                showEmptyState(true);
                return;
            }

            if (!hasTelemetry) {
                showEmptyState(true);
                return;
            }

            showEmptyState(false);

            const chartOptions = {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    y: { grid: { color: 'rgba(0,0,0,0.03)' }, ticks: { font: { size: 9 } } },
                    x: { grid: { display: false }, ticks: { font: { size: 9 } } }
                }
            };

            const createChart = (canvasId, config) => {
                const canvas = document.getElementById(canvasId);
                if (!canvas) {
                    console.warn(`Benchmarks canvas #${canvasId} was not found.`);
                    return null;
                }

                if (!datasetHasValues(config?.data)) {
                    setChartFallback(canvas, true);
                    return null;
                }

                setChartFallback(canvas, false);
                const chart = new Chart(canvas, config);
                this.charts.push(chart);
                chart.update();
                return chart;
            };

            // Chart A: Latency
            createChart('chartLatency', {
                type: 'bar',
                data: {
                    labels: latencyData.map(d => valueOf(d, 'label', 'Label') || 'Unknown'),
                    datasets: [{
                        data: latencyData.map(d => toNumber(valueOf(d, 'value', 'Value'))),
                        backgroundColor: '#6366f1',
                        borderRadius: 8
                    }]
                },
                options: chartOptions
            });

            // Chart B: Ingestion
            createChart('chartIngestion', {
                type: 'bar',
                data: {
                    labels: ingestionData.map(d => valueOf(d, 'label', 'Label') || 'Document'),
                    datasets: [{
                        data: ingestionData.map(d => toNumber(valueOf(d, 'value', 'Value'))),
                        backgroundColor: '#8b5cf6',
                        borderRadius: 8
                    }]
                },
                options: {
                    ...chartOptions,
                    scales: {
                        x: { grid: { display: false }, ticks: { font: { size: 9 } } },
                        y: { title: { display: true, text: 'Time (ms)', font: { size: 8 } } }
                    }
                }
            });

            // Chart C: Retrieval Similarity
            createChart('chartRetrieval', {
                type: 'line',
                data: {
                    labels: retrievalData.map(d => valueOf(d, 'label', 'Label') || ''),
                    datasets: [{
                        data: retrievalData.map(d => toNumber(valueOf(d, 'value', 'Value'))),
                        borderColor: '#10b981',
                        backgroundColor: 'rgba(16, 185, 129, 0.1)',
                        fill: true,
                        tension: 0.4
                    }]
                },
                options: {
                    ...chartOptions,
                    scales: {
                        x: { grid: { display: false }, ticks: { font: { size: 9 } } },
                        y: { min: 0, max: 1, title: { display: true, text: 'Similarity', font: { size: 8 } } }
                    }
                }
            });

            // Chart D: Throughput
            createChart('chartThroughput', {
                type: 'bar',
                data: {
                    labels: throughputData.map(d => valueOf(d, 'label', 'Label') || 'Unknown'),
                    datasets: [{
                        data: throughputData.map(d => toNumber(valueOf(d, 'value', 'Value'))),
                        backgroundColor: '#f59e0b',
                        borderRadius: 8
                    }]
                },
                options: chartOptions
            });

            // Chart E: Knowledge Density
            createChart('chartDensity', {
                type: 'doughnut',
                data: {
                    labels: densityData.map(d => valueOf(d, 'label', 'Label') || 'Untitled Document'),
                    datasets: [{
                        data: densityData.map(d => toNumber(valueOf(d, 'value', 'Value'))),
                        backgroundColor: ['#6366f1', '#8b5cf6', '#ec4899', '#f43f5e', '#f59e0b', '#10b981'],
                        borderWidth: 0
                    }]
                },
                options: {
                    ...chartOptions,
                    cutout: '70%',
                    plugins: { legend: { display: false } }
                }
            });

            // Chart F: Storage / Vector Footprint
            createChart('chartStorage', {
                type: 'bar',
                data: {
                    labels: storageData.map(d => valueOf(d, 'label', 'Label') || 'Untitled Document'),
                    datasets: [{
                        data: storageData.map(d => toNumber(valueOf(d, 'value', 'Value'))),
                        backgroundColor: '#ec4899',
                        borderRadius: 8
                    }]
                },
                options: {
                    ...chartOptions,
                    scales: {
                        x: { grid: { display: false }, ticks: { font: { size: 9 } } },
                        y: { title: { display: true, text: 'KB', font: { size: 8 } } }
                    }
                }
            });

        } catch (e) {
            if (window.showAlert) {
                window.showAlert('Visualization Error', 'Failed to initialize benchmark charts.', 'error');
            } else {
                console.error('Failed to initialize benchmark charts.', e);
            }
            showEmptyState(true);
        }
    }

    async switchSession(sessionId) {
        const wasChatView = this.activeView === 'chat';
        this.activeView = 'chat';
        this.showChatInput(true);
        this.charts.forEach(c => c.destroy());
        this.charts = [];

        if (this.currentSessionId === sessionId && wasChatView) return;

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
        this.updateExportLink();
    }

    async loadChatHistory() {
        this.updateDocumentBindingsUI(); // Clear all bindings first
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
            const relatedDocIds = data.relatedDocumentIds || data.RelatedDocumentIds || [];

            this.updateDocumentBindingsUI(relatedDocIds);

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
            this.showChatInput(true);
        }

        const question = this.chatInput.value.trim();
        if (!question) return;

        const topK = this.topKRange ? parseInt(this.topKRange.value) : 3;
        const isHighFidelity = this.researchModeToggle && this.researchModeToggle.checked;
        const selectedModel = this.modelSelector ? this.modelSelector.value : null;

        if (this.chatWindow.querySelector('.py-32')) this.chatWindow.innerHTML = '';

        this.appendMessage('user', question);
        this.chatInput.value = '';
        this.chatInput.style.height = 'auto';
        this.showLoading(true);
        this.appendTypingIndicator();
        this.activeRequestController = new AbortController();

        try {
            const data = await this.client.ask(question, this.currentSessionId, topK, null, selectedModel, isHighFidelity, this.activeRequestController.signal);

            this.removeTypingIndicator();

            if (!this.currentSessionId && data.sessionId) {
                this.currentSessionId = data.sessionId;
                window.history.pushState({}, '', `/?sessionId=${data.sessionId}`);
                if (this.sessionList) {
                    this.addSessionToSidebar(data.sessionId, data.sessionTitle || question);
                }
                this.updateExportLink();
            }

            this.appendMessage('ai', data.answer, data.latencyMs, data.messageId, data.sources);
        } catch (error) {
            this.removeTypingIndicator();
            if (error.name === 'AbortError') {
                this.appendMessage('ai', 'Request canceled by user.');
            } else {
                this.appendMessage('ai', 'Synthesis Error: ' + error.message);
            }
        } finally {
            this.activeRequestController = null;
            this.showLoading(false);
        }
    }

    cancelActiveRequest() {
        if (this.activeRequestController) {
            this.activeRequestController.abort();
        }
    }

    appendTypingIndicator() {
        const container = document.createElement('div');
        container.id = 'typingIndicator';
        container.className = 'max-w-4xl mx-auto flex gap-8 fade-in px-4 mb-8';

        container.innerHTML = `
            <div class="w-10 h-10 rounded-xl shrink-0 flex items-center justify-center border bg-zinc-50 dark:bg-zinc-900 border-zinc-200 dark:border-zinc-800 text-zinc-500 dark:text-zinc-400">
                <i data-lucide="bot" class="w-5 h-5"></i>
            </div>
            <div class="flex-1 flex items-center gap-1.5 pt-4">
                <div class="w-1.5 h-1.5 bg-indigo-500 rounded-full animate-bounce" style="animation-delay: 0s"></div>
                <div class="w-1.5 h-1.5 bg-indigo-500 rounded-full animate-bounce" style="animation-delay: 0.1s"></div>
                <div class="w-1.5 h-1.5 bg-indigo-500 rounded-full animate-bounce" style="animation-delay: 0.2s"></div>
            </div>
        `;

        this.chatWindow.appendChild(container);
        if (window.lucide) lucide.createIcons({ root: container });
        this.chatWindow.scrollTop = this.chatWindow.scrollHeight;
    }

    removeTypingIndicator() {
        const el = document.getElementById('typingIndicator');
        if (el) el.remove();
    }

    updateExportLink() {
        if (!this.exportBtn) return;

        if (this.currentSessionId) {
            this.exportBtn.href = `/Chat/ExportSession?sessionId=${this.currentSessionId}`;
            this.exportBtn.classList.remove('opacity-50', 'pointer-events-none');
            this.exportBtn.title = "Export scientific research synthesis";
        } else {
            this.exportBtn.href = "#";
            this.exportBtn.classList.add('opacity-50', 'pointer-events-none');
            this.exportBtn.title = "Active session required for export";
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
            const fallbackId = window.crypto?.randomUUID ? window.crypto.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
            const proofListId = `proofs-${messageId || fallbackId}`;
            this.sourceCache[proofListId] = sources;

            const inspectAll = document.createElement('button');
            inspectAll.className = 'px-3 py-1.5 rounded-lg bg-zinc-900 text-white dark:bg-white dark:text-zinc-950 border border-zinc-900 dark:border-white text-[10px] font-bold hover:opacity-90 transition-all flex items-center gap-2 shadow-sm';
            inspectAll.innerHTML = `<i data-lucide="list-checks" class="w-3 h-3"></i> Evidence (${sources.length})`;
            inspectAll.onclick = () => this.inspectSources(proofListId);
            sourceGrid.appendChild(inspectAll);

            sources.forEach((s, idx) => {
                const sourceId = `src-${messageId}-${idx}`;
                this.sourceCache[sourceId] = s;
                const badge = document.createElement('button');
                badge.className = 'px-3 py-1.5 rounded-lg bg-indigo-50/50 dark:bg-indigo-900/20 border border-indigo-100 dark:border-indigo-800/50 text-[10px] font-bold text-indigo-600 dark:text-indigo-400 hover:bg-indigo-100 dark:hover:bg-indigo-900/40 transition-all flex items-center gap-2 shadow-sm';
                badge.innerHTML = `<i data-lucide="file-text" class="w-3 h-3"></i> Proof [${idx + 1}]`;
                badge.onclick = () => this.inspectSources(proofListId, idx);
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

    inspectSources(sourceListId, activeIndex = 0) {
        const sources = this.sourceCache[sourceListId];
        if (!Array.isArray(sources) || sources.length === 0) return;
        this.toggleInspector(true);

        const normalized = sources.map((source, index) => ({
            index,
            content: source.content || source.Content || '',
            sourceDocument: source.sourceDocument || source.SourceDocument || 'Unknown Source',
            score: Number(source.score ?? source.Score ?? 0),
            documentId: source.documentId || source.DocumentId || '',
            chunkId: source.chunkId || source.ChunkId || '',
            chunkIndex: source.chunkIndex ?? source.ChunkIndex
        }));

        const averageScore = normalized.reduce((sum, source) => sum + source.score, 0) / normalized.length;

        this.inspectorContent.innerHTML = '';
        const wrapper = document.createElement('div');
        wrapper.className = 'space-y-5 animate-in fade-in slide-in-from-right-4 duration-300';

        const header = document.createElement('div');
        header.className = 'space-y-2';
        header.innerHTML = `
            <div class="flex items-center justify-between gap-4">
                <span class="text-[10px] font-bold text-indigo-600 dark:text-indigo-400 uppercase tracking-widest">Evidence Proof List</span>
                <span class="text-[10px] font-mono text-zinc-400 dark:text-zinc-500">${Math.round(averageScore * 100)}% Avg Match</span>
            </div>
            <p class="text-[11px] leading-relaxed text-zinc-500 dark:text-zinc-400">Retrieved chunks used as grounded context for this answer. These are the proof records that constrain the model output.</p>
        `;
        wrapper.appendChild(header);

        normalized.forEach((source) => {
            const scorePercent = Math.round(source.score * 100);
            const card = document.createElement('article');
            card.className = `rounded-2xl border p-4 space-y-3 ${source.index === activeIndex ? 'border-indigo-300 dark:border-indigo-700 bg-indigo-50/40 dark:bg-indigo-950/20' : 'border-zinc-200 dark:border-zinc-900 bg-zinc-50/50 dark:bg-zinc-900/30'}`;

            const title = document.createElement('div');
            title.className = 'flex items-start justify-between gap-3';
            title.innerHTML = `
                <div class="min-w-0">
                    <p class="text-[9px] font-bold uppercase tracking-widest text-zinc-400 dark:text-zinc-500">Proof ${source.index + 1}</p>
                    <h4 class="text-sm font-bold text-zinc-900 dark:text-white leading-tight truncate"></h4>
                </div>
                <span class="shrink-0 text-[10px] font-mono text-indigo-600 dark:text-indigo-400">${scorePercent}%</span>
            `;
            title.querySelector('h4').textContent = source.sourceDocument;
            card.appendChild(title);

            const meta = document.createElement('dl');
            meta.className = 'grid grid-cols-1 gap-1 text-[10px] text-zinc-500 dark:text-zinc-500 font-mono';
            const chunkLabel = Number.isInteger(Number(source.chunkIndex)) && Number(source.chunkIndex) >= 0 ? `#${source.chunkIndex}` : 'unknown';
            meta.innerHTML = `
                <div><dt class="inline uppercase tracking-widest">Chunk:</dt> <dd class="inline"></dd></div>
                <div><dt class="inline uppercase tracking-widest">Chunk ID:</dt> <dd class="inline break-all"></dd></div>
                <div><dt class="inline uppercase tracking-widest">Document ID:</dt> <dd class="inline break-all"></dd></div>
            `;
            meta.querySelectorAll('dd')[0].textContent = chunkLabel;
            meta.querySelectorAll('dd')[1].textContent = source.chunkId || 'not recorded';
            meta.querySelectorAll('dd')[2].textContent = source.documentId || 'not recorded';
            card.appendChild(meta);

            const quote = document.createElement('blockquote');
            quote.className = 'relative border-l-2 border-indigo-500/40 pl-4 text-[11px] text-zinc-700 dark:text-zinc-300 leading-relaxed whitespace-pre-wrap';
            quote.textContent = source.content;
            card.appendChild(quote);

            wrapper.appendChild(card);
        });

        const footer = document.createElement('div');
        footer.className = 'pt-4 border-t border-zinc-100 dark:border-zinc-900';
        footer.innerHTML = `
            <div class="flex items-center gap-2 text-[9px] text-zinc-400 dark:text-zinc-500 uppercase font-bold tracking-widest">
                <i data-lucide="shield-check" class="w-3 h-3"></i>
                <span>Grounded retrieval evidence, not model-invented citations</span>
            </div>
        `;
        wrapper.appendChild(footer);
        this.inspectorContent.appendChild(wrapper);
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
        if (this.loadingIndicator) this.loadingIndicator.classList.toggle('hidden', !show);
        if (this.sendBtn) {
            this.sendBtn.disabled = show;
            this.sendBtn.classList.toggle('hidden', show);
        }
        if (this.cancelBtn) {
            this.cancelBtn.classList.toggle('hidden', !show);
        }
    }

    showChatInput(show) {
        const inputContainer = this.chatInput?.closest('.p-8.border-t');
        if (inputContainer) {
            inputContainer.classList.toggle('hidden', !show);
        }
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
        if (!await window.showConfirm('Delete Research Thread', 'Are you sure you want to delete this research thread? This action is irreversible.')) return;

        try {
            const success = await this.client.deleteSession(sessionId);
            if (success) {
                element.remove();
                if (this.currentSessionId === sessionId) {
                    this.currentSessionId = null;
                    window.history.pushState({}, '', '/');
                    this.renderLobby();
                    this.updateExportLink();
                }
            } else {
                window.showAlert('Deletion Failed', 'Failed to delete research thread.', 'error');
            }
        } catch (error) {
            console.error(error);
            window.showAlert('System Error', 'An error occurred during deletion.', 'error');
        }
    }

    async handleDeleteDocument(documentId, element) {
        if (!await window.showConfirm('Delete Document', 'Are you sure you want to delete this document from the knowledge registry?')) return;

        try {
            const success = await this.client.deleteDocument(documentId);
            if (success) {
                element.remove();
            } else {
                window.showAlert('Deletion Failed', 'Failed to delete document.', 'error');
            }
        } catch (error) {
            console.error(error);
            window.showAlert('System Error', 'An error occurred during deletion.', 'error');
        }
    }
}
document.addEventListener('DOMContentLoaded', () => { window.ui = new RagUI(); });
