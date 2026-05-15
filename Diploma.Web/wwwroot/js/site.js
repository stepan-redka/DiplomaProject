// RagSystem Theme Management
window.themeManager = {
    toggle: function () {
        if (document.documentElement.classList.contains('dark')) {
            document.documentElement.classList.remove('dark');
            localStorage.theme = 'light';
        } else {
            document.documentElement.classList.add('dark');
            localStorage.theme = 'dark';
        }
        this.updateIcons();
    },
    updateIcons: function() {
        const isDark = document.documentElement.classList.contains('dark');
        document.querySelectorAll('.theme-sun').forEach(el => el.classList.toggle('hidden', isDark));
        document.querySelectorAll('.theme-moon').forEach(el => el.classList.toggle('hidden', !isDark));
    }
};

// Custom Dialog Logic
window.showConfirm = function(title, message) {
    return new Promise((resolve) => {
        const dialog = document.getElementById('customDialog');
        const titleEl = document.getElementById('dialogTitle');
        const messageEl = document.getElementById('dialogMessage');
        const confirmBtn = document.getElementById('dialogConfirmBtn');
        const cancelBtn = document.getElementById('dialogCancelBtn');
        const iconContainer = document.getElementById('dialogIconContainer');
        const icon = document.getElementById('dialogIcon');

        titleEl.innerText = title;
        messageEl.innerText = message;
        cancelBtn.classList.remove('hidden');
        confirmBtn.innerText = 'Confirm';
        
        iconContainer.className = 'w-10 h-10 rounded-xl flex items-center justify-center shrink-0 bg-indigo-50 dark:bg-indigo-500/10 text-indigo-600 dark:text-indigo-400';
        icon.setAttribute('data-lucide', 'alert-circle');
        if (window.lucide) lucide.createIcons({ root: iconContainer });

        const handleResolve = (val) => {
            dialog.classList.add('hidden');
            dialog.classList.remove('flex');
            resolve(val);
        };

        confirmBtn.onclick = () => handleResolve(true);
        cancelBtn.onclick = () => handleResolve(false);
        document.getElementById('dialogBackdrop').onclick = () => handleResolve(false);

        dialog.classList.remove('hidden');
        dialog.classList.add('flex');
    });
};

window.showAlert = function(title, message, type = 'info') {
    return new Promise((resolve) => {
        const dialog = document.getElementById('customDialog');
        const titleEl = document.getElementById('dialogTitle');
        const messageEl = document.getElementById('dialogMessage');
        const confirmBtn = document.getElementById('dialogConfirmBtn');
        const cancelBtn = document.getElementById('dialogCancelBtn');
        const iconContainer = document.getElementById('dialogIconContainer');
        const icon = document.getElementById('dialogIcon');

        titleEl.innerText = title;
        messageEl.innerText = message;
        cancelBtn.classList.add('hidden');
        confirmBtn.innerText = 'Dismiss';

        if (type === 'error') {
            iconContainer.className = 'w-10 h-10 rounded-xl flex items-center justify-center shrink-0 bg-red-50 dark:bg-red-500/10 text-red-600 dark:text-red-400';
            icon.setAttribute('data-lucide', 'x-circle');
        } else if (type === 'success') {
            iconContainer.className = 'w-10 h-10 rounded-xl flex items-center justify-center shrink-0 bg-emerald-50 dark:bg-emerald-500/10 text-emerald-600 dark:text-emerald-400';
            icon.setAttribute('data-lucide', 'check-circle');
        } else {
            iconContainer.className = 'w-10 h-10 rounded-xl flex items-center justify-center shrink-0 bg-indigo-50 dark:bg-indigo-500/10 text-indigo-600 dark:text-indigo-400';
            icon.setAttribute('data-lucide', 'info');
        }
        if (window.lucide) lucide.createIcons({ root: iconContainer });

        confirmBtn.onclick = () => {
            dialog.classList.add('hidden');
            dialog.classList.remove('flex');
            resolve();
        };

        dialog.classList.remove('hidden');
        dialog.classList.add('flex');
    });
};

window.confirmAction = async function(form, title, message) {
    if (await window.showConfirm(title, message)) {
        form.submit();
    }
};

document.addEventListener('DOMContentLoaded', () => {
    window.themeManager.updateIcons();
});
