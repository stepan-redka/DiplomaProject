/**
 * RagSystem Client-Side Orchestrator
 * High-fidelity 'v0' implementation for Tailwind/Lucide UI.
 */

class RagApp {
    constructor() {
        this.chatWindow = document.getElementById('chatWindow');
        this.chatInput = document.getElementById('chatInput');
        this.sendBtn = document.getElementById('sendBtn');
        this.fileInput = document.getElementById('fileInput');
        this.clearBtn = document.getElementById('clearBtn');
        this.loadingIndicator = document.getElementById('loadingIndicator');
        
        // Tuning and Paste UI
        this.topKRange = document.getElementById('topKRange');
        this.topKValue = document.getElementById('topKValue');
        this.pasteBtn = document.getElementById('pasteBtn');
        this.savePasteBtn = document.getElementById('savePasteBtn');
        this.cancelPasteBtn = document.getElementById('cancelPasteBtn');
        this.closePasteBtn = document.getElementById('closePasteBtn');
        this.pasteModal = document.getElementById('pasteModal');
        this.upsellModal = document.getElementById('upsellModal');
        
        this.initEventListeners();
        this.loadChatHistory();
        
        this.messageCount = 0;
    }

    initEventListeners() {
        this.sendBtn.addEventListener('click', () => this.handleAsk());
        this.chatInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') this.handleAsk();
        });

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
        try {
            const response = await fetch('/Chat/GetHistory');
            if (response.status === 401) {
                console.log('Guest mode: Skipping history load.');
                return;
            }
            
            if (response.ok) {
                const history = await response.json();
                if (history.length > 0) {
                    this.chatWindow.innerHTML = '';
                    history.forEach(msg => {
                        this.appendMessage(msg.role === 'assistant' ? 'ai' : 'user', msg.content);
                    });
                }
            }
        } catch (error) {
            console.error('Failed to load chat history:', error);
        }
    }

    async handleAsk() {
        const question = this.chatInput.value.trim();
        if (!question) return;

        const topK = this.topKRange ? parseInt(this.topKRange.value) : 3;

        this.appendMessage('user', question);
        this.chatInput.value = '';
        this.showLoading(true);

        try {
            const response = await fetch('/Chat/Ask', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ question: question, topK: topK })
            });

            if (!response.ok) throw new Error('Failed to communicate with AI service.');

            const data = await response.json();
            this.appendMessage('ai', data.answer, data.latencyMs);
            
            if (data.sources && data.sources.length > 0) {
                this.appendSources(data.sources);
            }

            // Identity Upsell: After 3 guest messages, nudge to sign up
            this.messageCount++;
            if (!data.isAuthenticated && this.messageCount >= 3) {
                setTimeout(() => {
                    this.upsellModal.classList.remove('hidden');
                }, 2000);
            }

        } catch (error) {
            this.appendMessage('ai', 'Error: ' + error.message);
        } finally {
            this.showLoading(false);
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
                alert('Failed to index text.');
            }
        } catch (error) {
            alert('Network error during indexing.');
        } finally {
            this.savePasteBtn.disabled = false;
            this.savePasteBtn.innerText = 'INDEX CONTENT';
        }
    }

    async handleClear() {
        if (!confirm('Destroy current knowledge collection? This action is irreversible.')) return;

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
            alert('Error clearing collection.');
        }
    }

    appendMessage(sender, text, latency) {
        const container = document.createElement('div');
        container.className = 'max-w-2xl mx-auto flex gap-6 fade-in';
        
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

        if (latency) {
            const stat = document.createElement('div');
            stat.className = 'flex items-center gap-2 mt-4';
            stat.innerHTML = `<span class="text-[9px] font-bold uppercase tracking-widest text-zinc-600">Latency: ${latency}ms</span>`;
            contentDiv.appendChild(stat);
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

    appendSources(sources) {
        const container = document.createElement('div');
        container.className = 'max-w-2xl mx-auto pl-14';
        
        const inner = document.createElement('div');
        inner.className = 'p-4 rounded-lg bg-zinc-900/50 border border-zinc-800/50 space-y-2';
        
        const title = document.createElement('h4');
        title.className = 'text-[10px] font-bold uppercase tracking-widest text-zinc-500 mb-2';
        title.innerText = 'Retrieval Sources';
        inner.appendChild(title);

        sources.forEach(s => {
            const item = document.createElement('div');
            item.className = 'flex items-center justify-between text-[11px] text-zinc-400';
            item.innerHTML = `<span>${s.sourceDocument}</span><span class="font-mono text-zinc-600">Score: ${s.score.toFixed(3)}</span>`;
            inner.appendChild(item);
        });

        container.appendChild(inner);
        this.chatWindow.appendChild(container);
        this.chatWindow.scrollTop = this.chatWindow.scrollHeight;
    }

    showLoading(show) {
        this.loadingIndicator.classList.toggle('hidden', !show);
        this.sendBtn.disabled = show;
    }
}

document.addEventListener('DOMContentLoaded', () => {
    window.app = new RagApp();
});