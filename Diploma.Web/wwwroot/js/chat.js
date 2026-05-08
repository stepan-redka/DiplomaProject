/**
 * RagSystem Client-Side Orchestrator
 * This module handles the asynchronous interaction between the Dashboard UI
 * and the .NET Controllers.
 */

class RagApp {
    constructor() {
        this.chatWindow = document.getElementById('chatWindow');
        this.chatInput = document.getElementById('chatInput');
        this.sendBtn = document.getElementById('sendBtn');
        this.fileInput = document.getElementById('fileInput');
        this.clearBtn = document.getElementById('clearBtn');
        this.loadingIndicator = document.getElementById('loadingIndicator');
        
        this.initEventListeners();
        this.loadChatHistory();
    }

    initEventListeners() {
        // Send message on click or Enter
        this.sendBtn.addEventListener('click', () => this.handleAsk());
        this.chatInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') this.handleAsk();
        });

        // Handle file upload
        this.fileInput.addEventListener('change', () => this.handleUpload());

        // Handle collection clearing
        this.clearBtn.addEventListener('click', () => this.handleClear());
    }

    /**
     * Loads past messages from the server.
     */
    async loadChatHistory() {
        try {
            const response = await fetch('/Chat/GetHistory');
            if (response.ok) {
                const history = await response.json();
                if (history.length > 0) {
                    // Clear the welcome message if we have real history
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

    /**
     * Sends a question to the ChatController/Ask endpoint.
     */
    async handleAsk() {
        const question = this.chatInput.value.trim();
        if (!question) return;

        this.appendMessage('user', question);
        this.chatInput.value = '';
        this.showLoading(true);

        try {
            const response = await fetch('/Chat/Ask', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ question: question })
            });

            if (!response.ok) throw new Error('Failed to communicate with AI service.');

            const data = await response.json();
            this.appendMessage('ai', data.answer, data.latencyMs);
            
            if (data.sources && data.sources.length > 0) {
                this.appendSources(data.sources);
            }

        } catch (error) {
            this.appendMessage('ai', 'Error: ' + error.message);
        } finally {
            this.showLoading(false);
        }
    }

    /**
     * Handles file upload to DocumentsController/Upload.
     */
    async handleUpload() {
        const file = this.fileInput.files[0];
        if (!file) return;

        const formData = new FormData();
        formData.append('file', file);
        
        // Anti-Forgery Token
        const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenElement ? tokenElement.value : '';
        
        this.appendMessage('ai', `Uploading and indexing ${file.name}...`);

        try {
            const response = await fetch('/Documents/Upload', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token
                },
                body: formData
            });

            if (response.ok) {
                window.location.reload(); // Refresh to show new documents in sidebar
            } else {
                const error = await response.text();
                this.appendMessage('ai', 'Upload failed: ' + error);
            }
        } catch (error) {
            this.appendMessage('ai', 'Network error during upload.');
        }
    }

    /**
     * Clears the user's document collection.
     */
    async handleClear() {
        if (!confirm('Are you sure you want to delete all uploaded knowledge?')) return;

        const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenElement ? tokenElement.value : '';

        try {
            const response = await fetch('/Documents/ClearCollection', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token
                }
            });

            if (response.ok) {
                window.location.reload();
            }
        } catch (error) {
            alert('Error clearing collection.');
        }
    }

    /**
     * Appends a message bubble to the chat window.
     * @param {'user' | 'ai'} sender 
     * @param {string} text 
     * @param {number} [latency]
     */
    appendMessage(sender, text, latency) {
        const msgDiv = document.createElement('div');
        msgDiv.className = `message message-${sender}`;
        
        // Clean text formatting (handle newlines)
        const formattedText = text.replace(/\n/g, '<br/>');
        msgDiv.innerHTML = formattedText;

        if (latency) {
            const latencySmall = document.createElement('small');
            latencySmall.className = 'd-block text-muted mt-2';
            latencySmall.style.fontSize = '0.7rem';
            latencySmall.innerText = `Response time: ${latency}ms`;
            msgDiv.appendChild(latencySmall);
        }

        this.chatWindow.appendChild(msgDiv);
        this.chatWindow.scrollTop = this.chatWindow.scrollHeight;
    }

    appendSources(sources) {
        const sourcesDiv = document.createElement('div');
        sourcesDiv.className = 'message message-ai bg-secondary bg-opacity-10 small py-2';
        sourcesDiv.style.fontSize = '0.8rem';
        sourcesDiv.innerHTML = '<strong>Sources:</strong><br/>' + 
            sources.map(s => `• ${s.sourceDocument} (Score: ${s.score.toFixed(2)})`).join('<br/>');
        
        this.chatWindow.appendChild(sourcesDiv);
        this.chatWindow.scrollTop = this.chatWindow.scrollHeight;
    }

    showLoading(show) {
        this.loadingIndicator.classList.toggle('d-none', !show);
        this.sendBtn.disabled = show;
    }
}

// Initialize the app when the DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.app = new RagApp();
});