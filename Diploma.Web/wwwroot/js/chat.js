/**
 * RagSystem Client-Side Orchestrator
 * High-fidelity 'v0' implementation for Tailwind/Lucide UI.
 */

class RagApp {
    constructor() {
        this.chatWindow = document.getElementById('chatWindow');
        this.chatInput = document.getElementById('chatInput');
        this.sendBtn = document.getElementById('sendBtn');
        this.cancelBtn = document.getElementById('cancelBtn');
        this.fileInput = document.getElementById('fileInput');
        this.clearBtn = document.getElementById('clearBtn');
        this.loadingIndicator = document.getElementById('loadingIndicator');
        
        // Session State
        this.mainWorkspace = document.getElementById('mainWorkspace');
        this.currentSessionId = this.mainWorkspace ? this.mainWorkspace.getAttribute('data-session-id') : null;
        
        // Tuning and Paste UI
        this.topKRange = document.getElementById('topKRange');
        this.topKValue = document.getElementById('topKValue');
        this.researchModeToggle = document.getElementById('researchModeToggle');
        this.pasteBtn = document.getElementById('pasteBtn');
        this.savePasteBtn = document.getElementById('savePasteBtn');
        this.cancelPasteBtn = document.getElementById('cancelPasteBtn');
        this.closePasteBtn = document.getElementById('closePasteBtn');
        this.pasteModal = document.getElementById('pasteModal');
        this.upsellModal = document.getElementById('upsellModal');
        
        // Cancellation state
        this.abortController = null;
        
        this.initEventListeners();
        this.loadChatHistory();
        
        this.messageCount = 0;
    }
    initEventListeners() {
        if (this.sendBtn) {
            this.sendBtn.addEventListener('click', () => this.handleAsk());
        }
        if (this.chatInput) {
            this.chatInput.addEventListener('keypress', (e) => {
                if (e.key === 'Enter') this.handleAsk();
            });
        }

        if (this.cancelBtn) {
            this.cancelBtn.addEventListener('click', () => this.handleCancel());
        }

        if (this.fileInput) {
            this.fileInput.addEventListener('change', () => this.handleUpload());
        }
        
        if (this.clearBtn) {
            this.clearBtn.addEventListener('click', () => this.handleClear());
        }

        if (this.topKRange) {
            this.topKRange.addEventListener('input', (e) => {
                this.topKValue.innerText = e.target.value;
            });
        }

        if (this.pasteBtn) {
            this.pasteBtn.addEventListener('click', () => {
                if (this.pasteModal) this.pasteModal.classList.remove('hidden');
            });
        }
        
        const closeActions = [this.cancelPasteBtn, this.closePasteBtn];
        closeActions.forEach(btn => {
            if (btn) btn.addEventListener('click', () => this.pasteModal.classList.add('hidden'));
        });

        if (this.savePasteBtn) {
            this.savePasteBtn.addEventListener('click', () => this.handlePaste());
        }
    }

    async loadChatHistory() {
        if (!this.currentSessionId || this.currentSessionId === "") {
            this.renderLobby();
            return;
        }

        try {
            const response = await fetch(`/Chat/GetSessionHistory?sessionId=${this.currentSessionId}`);
            if (response.status === 401) {
                console.log('Guest mode: Skipping history load.');
                this.renderLobby();
                return;
            }
            
            if (response.ok) {
                const history = await response.json();
                this.chatWindow.innerHTML = '';
                if (history && history.length > 0) {
                    history.forEach(msg => {
                        this.appendMessage(msg.role === 'assistant' ? 'ai' : 'user', msg.content, null, msg.id, msg.effectiveness);
                    });
                } else {
                    this.renderLobby();
                }
            }
        } catch (error) {
            console.error('Failed to load chat history:', error);
            window.showAlert('History Unavailable', 'Could not load chat history. Starting fresh session.', 'error');
            this.renderLobby();
        }
    }

    renderLobby() {
        this.chatWindow.innerHTML = `
            <div class="max-w-2xl mx-auto py-24 text-center space-y-8 fade-in">
                <div class="relative inline-block">
                    <div class="absolute -inset-4 bg-white/10 blur-2xl rounded-full"></div>
                    <div class="relative w-20 h-20 bg-white rounded-3xl flex items-center justify-center mx-auto shadow-2xl rotate-3 hover:rotate-0 transition-transform duration-500">
                        <i data-lucide="sparkles" class="w-10 h-10 text-black"></i>
                    </div>
                </div>
                <div class="space-y-4">
                    <h1 class="text-4xl font-bold text-white tracking-tight">New Research Project</h1>
                    <p class="text-zinc-500 text-sm max-w-md mx-auto leading-relaxed">
                        Establish a knowledge context by selecting sources from the registry or initiating a raw stream ingestion.
                    </p>
                </div>
                <div class="flex items-center justify-center gap-4 pt-4">
                    <div class="px-4 py-2 rounded-full bg-zinc-900 border border-zinc-800 flex items-center gap-2">
                        <div class="w-1.5 h-1.5 rounded-full bg-emerald-500"></div>
                        <span class="text-[10px] font-bold uppercase tracking-widest text-zinc-400">Environment Ready</span>
                    </div>
                </div>
            </div>
        `;
        if (window.lucide) {
            lucide.createIcons({ root: this.chatWindow });
        }
    }

    handleCancel() {
        if (this.abortController) {
            this.abortController.abort();
        }
    }

    async handleAsk() {
        const question = this.chatInput.value.trim();
        if (!question) return;

        const topK = this.topKRange ? parseInt(this.topKRange.value) : 3;
        const isResearch = this.researchModeToggle ? this.researchModeToggle.checked : true;
        const intent = isResearch ? 1 : 0; // 1 = Research, 0 = General

        // Clear lobby if first message
        if (this.chatWindow.querySelector('.py-24')) {
            this.chatWindow.innerHTML = '';
        }

        this.appendMessage('user', question);
        this.chatInput.value = '';

        // Create a fresh AbortController for this request
        this.abortController = new AbortController();
        this.showLoading(true);

        try {
            const response = await fetch('/Chat/Ask', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ 
                    question: question, 
                    sessionId: this.currentSessionId,
                    topK: topK,
                    intent: intent
                }),
                signal: this.abortController.signal
            });

            // 499 = server confirmed the client-initiated cancellation
            if (response.status === 499) {
                this.appendCancelMessage();
                return;
            }

            if (!response.ok) throw new Error('Failed to communicate with AI service.');

            const data = await response.json();
            
            // If this was a new session, update the state and URL
            if (!this.currentSessionId && data.sessionId) {
                this.currentSessionId = data.sessionId;
                const newUrl = window.location.protocol + "//" + window.location.host + window.location.pathname + '?sessionId=' + data.sessionId;
                window.history.pushState({path:newUrl},'',newUrl);
            }

            this.appendMessage('ai', data.answer, data.latencyMs, data.messageId);
            
            if (data.sources && data.sources.length > 0) {
                this.appendSources(data.sources, data.messageId);
            }

            // Identity Upsell: After 3 guest messages, nudge to sign up
            this.messageCount++;
            if (!data.isAuthenticated && this.messageCount >= 3) {
                setTimeout(() => {
                    this.upsellModal.classList.remove('hidden');
                }, 2000);
            }

        } catch (error) {
            if (error.name === 'AbortError') {
                // User clicked Cancel — show a soft informational note, not a red error
                this.appendCancelMessage();
            } else {
                this.appendMessage('ai', 'Error: ' + error.message);
            }
        } finally {
            this.abortController = null;
            this.showLoading(false);
        }
    }

    appendCancelMessage() {
        const container = document.createElement('div');
        container.className = 'max-w-2xl mx-auto pl-14 fade-in';
        container.innerHTML = `
            <p class="text-[11px] text-zinc-600 italic flex items-center gap-1.5">
                <i data-lucide="circle-slash" class="w-3 h-3"></i>
                Query canceled.
            </p>
        `;
        this.chatWindow.appendChild(container);
        if (window.lucide) lucide.createIcons({ root: container });
        this.chatWindow.scrollTop = this.chatWindow.scrollHeight;
    }

    async handleFeedback(messageId, effectiveness) {
        try {
            const response = await fetch('/Chat/SetFeedback', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ messageId, effectiveness })
            });

            if (response.ok) {
                // Visual confirmation
                const container = document.querySelector(`[data-message-id="${messageId}"]`);
                if (container) {
                    const buttons = container.querySelectorAll('.feedback-btn');
                    buttons.forEach(btn => {
                        btn.classList.remove('text-emerald-400', 'text-red-400', 'bg-zinc-800');
                        btn.classList.add('opacity-50');
                    });
                    
                    const activeBtn = container.querySelector(`[data-feedback="${effectiveness}"]`);
                    if (activeBtn) {
                        activeBtn.classList.remove('opacity-50');
                        activeBtn.classList.add(effectiveness === 1 ? 'text-emerald-400' : 'text-red-400', 'bg-zinc-800');
                    }
                }
            }
        } catch (error) {
            console.error('Failed to save feedback:', error);
            window.showAlert('Feedback Failed', 'Could not save your feedback. Please try again.', 'error');
        }
    }

    async handleUpload() {
        const files = this.fileInput.files;
        if (!files || files.length === 0) return;

        const formData = new FormData();
        for (let i = 0; i < files.length; i++) {
            formData.append('files', files[i]);
        }
        
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        
        this.appendMessage('ai', `Indexing ${files.length} document(s)...`);

        try {
            const response = await fetch('/Documents/Upload', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token },
                body: formData
            });

            if (response.status === 401) {
                this.upsellModal.classList.remove('hidden');
                return;
            }

            if (response.ok) {
                window.location.reload();
            } else {
                const error = await response.text();
                this.appendMessage('ai', 'Indexing failed: ' + error);
            }
        } catch (error) {
            this.appendMessage('ai', 'Network error during upload.');
        }
    }

    async handlePaste() {
        const content = document.getElementById('manualContent').value.trim();
        const docName = document.getElementById('manualDocName').value.trim();

        if (!content) return;

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

        this.savePasteBtn.disabled = true;
        this.savePasteBtn.innerText = 'INDEXING...';

        try {
            const response = await fetch('/Documents/PasteText', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({ content: content, documentName: docName })
            });

            if (response.status === 401) {
                this.upsellModal.classList.remove('hidden');
                return;
            }

            if (response.ok) {
                window.location.reload();
            } else {
                window.showAlert('Indexing Failed', 'Failed to index text.', 'error');
            }
        } catch (error) {
            window.showAlert('System Error', 'Network error during indexing.', 'error');
        } finally {
            this.savePasteBtn.disabled = false;
            this.savePasteBtn.innerText = 'INDEX CONTENT';
        }
    }

    async handleClear() {
        if (!await window.showConfirm('Clear Collection', 'Destroy current knowledge collection? This action is irreversible.')) return;

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

        try {
            const response = await fetch('/Documents/ClearCollection', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token }
            });

            if (response.status === 401) {
                this.upsellModal.classList.remove('hidden');
                return;
            }

            if (response.ok) {
                window.location.reload();
            }
        } catch (error) {
            window.showAlert('System Error', 'Error clearing collection.', 'error');
        }
    }

    appendMessage(sender, text, latency, messageId, effectiveness = 0) {
        const container = document.createElement('div');
        container.className = 'max-w-2xl mx-auto flex gap-6 fade-in group';
        if (messageId) container.setAttribute('data-message-id', messageId);
        
        const iconDiv = document.createElement('div');
        iconDiv.className = `w-8 h-8 rounded shrink-0 flex items-center justify-center border ${sender === 'ai' ? 'bg-zinc-900 border-zinc-800' : 'bg-zinc-100 border-white'}`;
        
        const icon = document.createElement('i');
        icon.setAttribute('data-lucide', sender === 'ai' ? 'sparkles' : 'user');
        icon.className = `w-4 h-4 ${sender === 'ai' ? 'text-zinc-400' : 'text-black'}`;
        
        iconDiv.appendChild(icon);
        container.appendChild(iconDiv);

        const contentDiv = document.createElement('div');
        contentDiv.className = 'flex-1 space-y-2';
        
        const p = document.createElement('p');
        p.className = `text-sm leading-relaxed ${sender === 'ai' ? 'text-zinc-300' : 'text-zinc-100 font-medium'}`;
        p.innerHTML = text.replace(/\n/g, '<br/>');
        
        contentDiv.appendChild(p);

        if (sender === 'ai' && messageId) {
            const actionsDiv = document.createElement('div');
            actionsDiv.className = 'flex items-center justify-between mt-4';
            
            // Stats
            const stats = document.createElement('div');
            stats.className = 'flex items-center gap-4';
            if (latency) {
                stats.innerHTML = `<span class="text-[9px] font-bold uppercase tracking-widest text-zinc-600">Latency: ${latency}ms</span>`;
            }
            actionsDiv.appendChild(stats);

            // Feedback Loop
            const feedback = document.createElement('div');
            feedback.className = 'flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity';
            
            const createFeedbackBtn = (type, iconName, value) => {
                const btn = document.createElement('button');
                btn.className = `feedback-btn p-1.5 rounded-md hover:bg-zinc-800 transition-colors text-zinc-600 ${effectiveness === value ? (value === 1 ? 'text-emerald-400 bg-zinc-800' : 'text-red-400 bg-zinc-800') : ''}`;
                btn.setAttribute('data-feedback', value);
                btn.innerHTML = `<i data-lucide="${iconName}" class="w-3.5 h-3.5"></i>`;
                btn.onclick = () => this.handleFeedback(messageId, value);
                return btn;
            };

            feedback.appendChild(createFeedbackBtn('positive', 'thumbs-up', 1));
            feedback.appendChild(createFeedbackBtn('negative', 'thumbs-down', 2));
            actionsDiv.appendChild(feedback);

            contentDiv.appendChild(actionsDiv);
        }

        container.appendChild(contentDiv);
        this.chatWindow.appendChild(container);
        
        if (window.lucide) {
            lucide.createIcons({
                root: container
            });
        }
        this.chatWindow.scrollTop = this.chatWindow.scrollHeight;
    }

    appendSources(sources, messageId) {
        const container = document.createElement('div');
        container.className = 'max-w-2xl mx-auto pl-14 fade-in';
        
        const toggleBtn = document.createElement('button');
        toggleBtn.className = 'flex items-center gap-2 text-[10px] font-bold uppercase tracking-widest text-zinc-500 hover:text-zinc-300 transition-colors mb-2';
        toggleBtn.innerHTML = `<i data-lucide="chevron-down" class="w-3 h-3 transition-transform"></i><span>View Source Transparency</span>`;
        
        const sourcesDiv = document.createElement('div');
        sourcesDiv.className = 'hidden p-4 rounded-xl bg-zinc-900/50 border border-zinc-800/50 space-y-4';
        
        sources.forEach(s => {
            const item = document.createElement('div');
            item.className = 'space-y-1.5';
            item.innerHTML = `
                <div class="flex items-center justify-between">
                    <span class="text-[10px] font-bold text-zinc-400 truncate pr-4">${s.sourceDocument}</span>
                    <span class="text-[9px] font-mono text-zinc-600 shrink-0">Score: ${s.score.toFixed(3)}</span>
                </div>
                <p class="text-[11px] text-zinc-500 italic leading-relaxed line-clamp-2">"${s.content}"</p>
            `;
            sourcesDiv.appendChild(item);
        });

        toggleBtn.onclick = () => {
            const isHidden = sourcesDiv.classList.toggle('hidden');
            toggleBtn.querySelector('i').classList.toggle('rotate-180', !isHidden);
        };

        container.appendChild(toggleBtn);
        container.appendChild(sourcesDiv);
        this.chatWindow.appendChild(container);
        
        if (window.lucide) {
            lucide.createIcons({ root: container });
        }
        this.chatWindow.scrollTop = this.chatWindow.scrollHeight;
    }

    showLoading(show) {
        this.loadingIndicator.classList.toggle('hidden', !show);
        this.sendBtn.disabled = show;
        this.sendBtn.classList.toggle('hidden', show);
        if (this.cancelBtn) {
            this.cancelBtn.classList.toggle('hidden', !show);
        }
    }
}

document.addEventListener('DOMContentLoaded', () => {
    window.app = new RagApp();
});