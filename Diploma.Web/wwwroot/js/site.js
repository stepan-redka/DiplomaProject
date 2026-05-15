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

document.addEventListener('DOMContentLoaded', () => {
    window.themeManager.updateIcons();
});
