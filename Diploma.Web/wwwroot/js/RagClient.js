/**
 * RagClient.js
 * Stateless API Client for the RagSystem.
 * Encapsulates all network communication.
 */
export class RagClient {
    async ask(question, sessionId = null, topK = 3, intent = 1, selectedModel = null) {
        const response = await fetch('/Chat/Ask', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ question, sessionId, topK, intent, selectedModel })
        });
        if (!response.ok) throw new Error('Query failed');
        return await response.json();
    }

    async getHistory(sessionId) {
        const response = await fetch(`/Chat/GetSessionHistory?sessionId=${sessionId}`);
        if (!response.ok) throw new Error('History retrieval failed');
        return await response.json();
    }

    async deleteSession(sessionId) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const response = await fetch(`/Chat/DeleteSession?id=${sessionId}`, {
            method: 'POST',
            headers: { 'RequestVerificationToken': token }
        });
        return response.ok;
    }

    async deleteDocument(documentId) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const response = await fetch(`/Documents/Delete?id=${documentId}`, {
            method: 'POST',
            headers: { 'RequestVerificationToken': token }
        });
        return response.ok;
    }

    async setFeedback(messageId, effectiveness) {
        const response = await fetch('/Chat/SetFeedback', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ messageId, effectiveness })
        });
        return response.ok;
    }

    async uploadFiles(formData) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const response = await fetch('/Documents/Upload', {
            method: 'POST',
            headers: { 'RequestVerificationToken': token },
            body: formData
        });
        return response;
    }

    async clearCollection() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const response = await fetch('/Documents/ClearCollection', {
            method: 'POST',
            headers: { 'RequestVerificationToken': token }
        });
        return response.ok;
    }

    // --- LAB & ANALYTICS ---
    async getLabView(viewName) {
        const response = await fetch(`/Lab/${viewName}`);
        if (!response.ok) throw new Error(`Failed to load ${viewName} view.`);
        return await response.text();
    }
}
