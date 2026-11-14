(() => {
    const button = document.getElementById('login-password-toggle');
    const input = document.getElementById('login-password');
    button?.addEventListener('click', () => {
        const visible = input.type === 'password';
        input.type = visible ? 'text' : 'password';
        button.textContent = visible ? 'Ocultar' : 'Mostrar';
        button.setAttribute('aria-pressed', String(visible));
    });
})();
